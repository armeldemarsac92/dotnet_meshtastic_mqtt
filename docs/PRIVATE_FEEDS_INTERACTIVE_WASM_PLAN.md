# Private Feeds Via Interactive WebAssembly

This document plans Option B for MeshBoard:

- keep the main application as a Blazor Web App
- keep public/shared feeds on the existing server/worker path
- move private-feed key handling and private payload decryption into Interactive WebAssembly components running in the browser

This is the preferred direction when private-channel security is more important than feature symmetry with public feeds.

If the product direction shifts toward reducing overall Blazor Server circuit load for most user-facing pages, see the broader `docs/BROWSER_FIRST_ARCHITECTURE_PLAN.md`.

## Objective

For private or sensitive feeds:

- the browser owns the decryption key
- the server does not persist the decryption key
- the worker does not decrypt private payloads
- decrypted private content is not stored in server-side history by default

For public feeds:

- the server/worker path remains authoritative
- public ingest can still be optimized and shared

## Why Option B

Interactive WebAssembly is the only Blazor mode that naturally lets component logic execute in the browser.

Microsoft's render mode guidance confirms:

- `Interactive WebAssembly` renders on the client
- Blazor Web Apps can support both `Interactive Server` and `Interactive WebAssembly`
- components using `Interactive WebAssembly` must be built from a separate client project

Sources:

- Microsoft Learn, Blazor render modes:
  https://learn.microsoft.com/en-us/aspnet/core/blazor/components/render-modes?view=aspnetcore-10.0

Relevant points from the docs:

- `Interactive WebAssembly` is client-side rendering in the browser
- a separate client project is required for WebAssembly components
- client components should be built from the client project so they are included in the browser bundle

## Proposed Hybrid Architecture

### Public Path

Keep the current server/worker architecture for feeds classified as `PublicShared`:

- MQTT worker runtime
- bounded queue
- decode/project on the server
- shared public projections
- push updates to users

### Private Path

For feeds classified as `SensitiveIsolated`:

- private feed UI runs as Interactive WebAssembly
- browser receives encrypted packets only
- browser decrypts and decodes packets locally
- browser renders private content locally
- server stores no private keys and no decrypted private payload text

## Render Mode Strategy

Do not convert the whole app to Interactive WebAssembly.

Use per-page or per-component Interactive WebAssembly only for the private-feed experience.

Recommended shape:

- current authenticated shell stays server-side
- private feed pages/components live in the client project
- these pages/components are marked with `@rendermode InteractiveWebAssembly`

### Hurdle: Separate Client Project

Microsoft's docs explicitly require a separate client project for Interactive WebAssembly components.

This means:

- add a new `.Client` project if one does not exist
- private feed pages/components must be moved or created there
- shared contracts stay in `Contracts`
- browser-safe services must live in the client project or a shared browser-safe library

Source:

- Microsoft Learn, Blazor render modes:
  https://learn.microsoft.com/en-us/aspnet/core/blazor/components/render-modes?view=aspnetcore-10.0

## Prerendering Hurdle

Interactive components are prerendered by default.

For private-feed pages, prerendering is the wrong default because:

- prerender runs on the server first
- client-only services are unavailable during prerender
- secure private keys must never be required during server prerender
- components render twice, which complicates private realtime initialization

Recommended rule:

- disable prerendering for private Interactive WebAssembly pages/components

Source:

- Microsoft Learn, prerendering:
  https://learn.microsoft.com/en-us/aspnet/core/blazor/components/prerender?view=aspnetcore-10.0

Relevant points from the docs:

- interactive components prerender by default
- prerender causes components to render twice
- client-only services fail during prerender
- prerender can be disabled for `InteractiveWebAssemblyRenderMode`

## Authentication Hurdle

Private Interactive WebAssembly components still need authenticated user/workspace context.

Microsoft exposes a supported path for serializing server authentication state to Interactive WebAssembly components:

- `AddAuthenticationStateSerialization(...)` on the server
- `AddAuthenticationStateDeserialization(...)` in the client project

Sources:

- Microsoft Learn API docs:
  https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.webassemblyrazorcomponentsbuilderextensions.addauthenticationstateserialization?view=aspnetcore-10.0
  https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.webassemblyauthenticationservicecollectionextensions.addauthenticationstatedeserialization?view=aspnetcore-10.0

Recommended rule:

- use serialized auth state only for user/workspace identity
- do not serialize secret material into prerendered or persistent auth state

## Crypto Implementation Options

### Preferred: Web Crypto API Via JS Interop

Use the browser's `SubtleCrypto` implementation from the Interactive WebAssembly components via JSImport/JS interop or a small JS wrapper.

Why:

- browser-native crypto implementation
- available in secure contexts
- available in Web Workers
- supports AES-CTR, which Meshtastic requires

Sources:

- MDN Web Crypto API:
  https://developer.mozilla.org/en-US/docs/Web/API/Web_Crypto_API
- MDN AES-CTR params:
  https://developer.mozilla.org/en-US/docs/Web/API/AesCtrParams
- Microsoft Learn, JSImport/JSExport with Blazor/WebAssembly:
  https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-interop/wasm-browser-app?view=aspnetcore-10.0

Important note:

- MDN recommends authenticated encryption in general
- Meshtastic payload compatibility still requires AES-CTR, so this path must follow the protocol exactly rather than inventing a new cipher mode

### Secondary Option: Pure Managed .NET Crypto In WASM

This may be possible, but it is not the preferred design.

Reason:

- it is less browser-native
- it offers no security advantage over Web Crypto
- it does not help with keeping key material in browser-managed crypto objects

This document does not recommend it as the primary path.

### Recommended Key Handling Rule

- key enters the browser from direct user input
- key stays in browser memory only by default
- no server persistence
- no server log exposure

Optional later enhancement:

- encrypted browser-side persistence using IndexedDB and a user passphrase

Not in the first slice.

## Worker/Threading Options For Crypto

### Preferred: Browser Web Worker

Use a dedicated Web Worker for:

- packet decrypt
- protobuf decode
- burst processing

Reason:

- avoids UI-thread stalls
- keeps the secure path isolated
- Web Crypto is available in workers

Source:

- MDN Web Crypto API:
  https://developer.mozilla.org/en-US/docs/Web/API/Web_Crypto_API

### Optional: .NET WebAssembly Multithreading

This is possible, but it adds hosting constraints.

Microsoft's server-side configuration exposes `ServeMultithreadingHeaders`, which adds headers needed for `SharedArrayBuffer`.
The docs note that enabling this can restrict other JavaScript APIs.

Source:

- Microsoft Learn API docs:
  https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.components.webassembly.server.webassemblycomponentsendpointoptions?view=aspnetcore-10.0

Recommended rule:

- do not make WASM multithreading a first-step dependency
- start with a JS Web Worker
- evaluate .NET multithreading only if measured browser CPU load justifies it

## WebSocket / MQTT Implementation Options

### Preferred: MQTT.js In The Browser

Use MQTT.js from the browser-side private-feed runtime.

Reason:

- it explicitly supports browser execution
- its docs explicitly state that browsers only support MQTT over `ws://` or `wss://`
- it is a mature browser MQTT client

Source:

- MQTT.js README:
  https://github.com/mqttjs/MQTT.js

Important browser constraint from MQTT.js:

- browsers only support MQTT over WebSockets
- detailed connection errors are limited in browsers for security reasons

### Secondary Option: Custom Browser WebSocket Bridge

If the target broker does not support MQTT over WebSockets:

- add a dedicated bridge/proxy that exposes `wss://` to the browser
- the bridge speaks raw MQTT/TCP upstream

This preserves the client-owned key model while avoiding direct browser dependence on the upstream broker's transport support.

### Not Recommended Initially: MQTTnet In Browser WASM

I did not verify an official supported `browser-wasm` path for using the current `MQTTnet` stack directly from Interactive WebAssembly in this codebase.
MQTT.js has explicit browser guidance. That makes it the lower-risk first implementation.

## Critical External Dependency

Option B depends on having a WebSocket-capable MQTT path.

### Hurdle: Meshtastic Public Broker WSS Support Is Not Confirmed

I did not find official Meshtastic documentation confirming that `mqtt.meshtastic.org` exposes a `wss://` endpoint for browser clients.

This means Option B must be planned with a fallback.

### Verified Example Of A Meshtastic-Compatible Broker With WebSockets

The community Meshtastic broker `mqtt.meshnet.si` explicitly documents:

- MQTT on `1883`
- TLS MQTT on `8883`
- WebSockets on `8884`

Source:

- `mqtt.meshnet.si`:
  https://mqtt.meshnet.si/

This proves the transport model is practical for Meshtastic-style brokers even if the official public broker doesn't expose it.

## Recommended Fallback If Official Broker Lacks WSS

Fallback order:

1. preferred: use a WebSocket-capable MQTT endpoint for private feeds
2. otherwise: add a MeshBoard-owned secure MQTT WebSocket bridge
3. last resort: keep private feeds isolated in the worker, but still do not store raw keys in the normal database

If the fallback reaches step 3:

- private feeds remain server-side isolated
- public feeds can still evolve toward shared ingest
- private-key security still improves because secrets move to a secure store instead of normal SQL columns

## UI/UX Consequences

Private-feed pages must be explicit about their semantics.

Recommended labels:

- `Client decrypted`
- `Not stored on server`
- `Key kept in browser memory`

Expected feature tradeoffs for private mode:

- no server-side private search
- no server-side private payload history by default
- no shared private analytics
- private content disappears after reload unless the user re-enters the key

These are acceptable tradeoffs for the security objective.

## Codebase Impact

### New Client Project

Recommended:

- `src/MeshBoard.Web.Client/`

This project would host:

- private-feed Interactive WebAssembly components
- browser-side MQTT wrapper interop
- browser-side crypto/decode services
- browser worker registration/bootstrap

### Server Changes

Update:

- `src/MeshBoard.Web/Program.cs`

To support:

- `AddInteractiveWebAssemblyComponents()`
- `AddInteractiveWebAssemblyRenderMode()`
- auth state serialization for WASM components

### Current Worker Changes

Current worker remains authoritative for public feeds only.

It must eventually:

- stop decrypting private-feed traffic
- stop persisting private keys from topic presets/profile defaults in ordinary tables
- stop writing decrypted private payload previews to ordinary message history

### Current UI Changes

Likely new or split pages:

- `PrivateMessages.razor`
- `PrivateTopics.razor`
- `PrivateKeyPrompt.razor`

Current shared pages should not silently mix server-decrypted and client-decrypted private content.

## Recommended First Implementation Slice

Do not start with full mixed-feed UI integration.

Start with one isolated vertical slice:

1. add client project for Interactive WebAssembly
2. enable Interactive WebAssembly in the host
3. add auth state serialization/deserialization
4. build a browser-only private key prompt
5. build a browser-only private message stream using MQTT over WebSockets
6. decrypt and render private text locally only

Acceptance for the first slice:

- private key never reaches server persistence
- private decrypted payload never reaches server persistence
- authenticated user can open a private-feed page and see decrypted live content in-browser
- reload clears the key unless the user re-enters it

## Decision Summary

Option B is viable, but only under these rules:

- keep the main app as a Blazor Web App
- keep public/shared feeds on the current server/worker path
- use Interactive WebAssembly only for private/sensitive feeds
- prefer Web Crypto + browser Web Worker
- prefer MQTT.js over `wss://`
- plan for a bridge/proxy if the upstream broker lacks WebSocket support

This keeps the product web-first, scalable for public telemetry, and materially stronger for private-channel secrecy.
