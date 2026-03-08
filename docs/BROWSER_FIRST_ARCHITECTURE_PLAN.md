# Browser-First Architecture Plan

This document defines the more aggressive alternative to the current Blazor Server-heavy model.

It is written in response to a legitimate scaling concern:

- Blazor Server puts user interaction state, rendering, and live component lifetime on the server
- this app is realtime and map-heavy
- server circuit load will become an avoidable bottleneck before the rest of the backend is fully optimized

The target of this plan is:

- browser-first UI execution
- backend-owned public ingest and projections
- browser-owned private decryption keys
- minimal reliance on Blazor Server circuits for end-user pages

This plan is broader than `docs/PRIVATE_FEEDS_INTERACTIVE_WASM_PLAN.md`.
That document explains how to add private Interactive WebAssembly features within the current app.
This document explains how to move the whole product toward a browser-first model.

## Goal

Reduce web-tier server load by moving user-facing interaction and rendering to the browser while preserving:

- efficient shared ingest for public feeds
- strict isolation for sensitive/private feeds
- durable backend projections for public telemetry
- authenticated multi-user access control

## Strategic Decision

Long term, MeshBoard should not stay primarily `Interactive Server`.

It should evolve toward:

- a browser-first Blazor frontend
- backend APIs for data and commands
- a public ingestion worker
- an MQTT WebSocket bridge for browser-side MQTT use cases

In practical terms:

- public/shared pages become browser-rendered and API-backed
- private/sensitive feeds become browser-owned and client-decrypted
- server-side circuits are reduced to transitional or admin-only surfaces

## Recommended Frontend Mode Choice

### Preferred Direction

Use a Blazor Web App with user-facing pages running primarily in `Interactive WebAssembly`.

This is the best fit if you want to stay in the Blazor ecosystem while reducing server load.

Microsoft's render mode documentation confirms:

- `Interactive WebAssembly` runs on the client
- a Blazor Web App can enable both server and WebAssembly interactivity
- a separate client project is required for Interactive WebAssembly components

Source:

- Microsoft Learn, Blazor render modes:
  https://learn.microsoft.com/en-us/aspnet/core/blazor/components/render-modes?view=aspnetcore-10.0

### Why Not Stay Primarily Interactive Server

Blazor Server is structurally expensive for this workload:

- one live circuit per connected tab
- server memory per interactive session
- server-side event and render handling per session
- extra reconnect and lifecycle complexity for live pages

Those tradeoffs are reasonable for internal tools or moderate concurrency.
They are less appropriate for a public live telemetry product targeting high concurrent usage.

### Why Not Full Standalone WebAssembly Immediately

A full standalone WASM rewrite is possible, but not required as the first step.

Using a Blazor Web App with a strong `.Client` project gives:

- a controlled migration path
- the ability to keep some server-side routes while moving user-facing features client-side
- reuse of the current hosting/auth structure during transition

## Target Architecture

### Public Path

`MQTT -> Public Ingestion Worker -> Shared Public Projections -> API/Push -> Browser`

Properties:

- one canonical ingest path for public telemetry
- public packets are ingested once
- backend owns retention, projections, and aggregation
- browser consumes public read models via APIs and push channels

### Private Path

`Browser -> MQTT over WSS -> Ciphertext -> Browser decrypt/decode -> Browser-only private rendering`

Properties:

- private keys stay in browser memory
- server does not persist private keys
- server does not persist decrypted private payload text
- private content is not part of shared backend projections

### Backend Responsibilities

The backend still exists and still matters.

It should own:

- authentication and session issuance
- user/workspace profiles and preferences
- public feed projections and history
- public topic discovery
- authorization
- public command routing
- bridge infrastructure when direct WSS is unavailable

It should not own:

- private feed keys
- decrypted private message content

## MQTT Bridge Design

The browser cannot open raw MQTT/TCP sockets.
Browser MQTT requires `ws://` or `wss://`.

The MQTT.js docs explicitly state browser support is over WebSockets only.

Source:

- MQTT.js README:
  https://github.com/mqttjs/MQTT.js

This creates two possible bridge models.

### Option 1: Direct Upstream WSS

If the target broker exposes `wss://`, the browser connects directly.

Pros:

- simplest private receive path
- no MeshBoard bridge in the private data plane
- strongest confidentiality for inbound private traffic

Cons:

- depends on upstream broker capabilities
- broker credentials, quotas, and connection behavior are exposed to browser clients
- backend has less control over connection policy

### Option 2: MeshBoard MQTT WebSocket Bridge

If the upstream broker does not expose `wss://`, add a MeshBoard bridge that exposes browser-safe WebSockets.

This is the realistic default if official Meshtastic infrastructure does not provide browser-facing MQTT transport.

#### Bridge Sub-Option A: Protocol-Aware Bridge

The bridge parses MQTT and can enforce:

- user authentication
- workspace authorization
- topic allowlists
- quotas and rate limits
- upstream connection pooling

Pros:

- operationally strong
- easier to secure and control
- best for multi-user governance

Cons:

- the bridge can see publish payloads
- for outbound private sends, that means the bridge sees plaintext unless the client constructs and encrypts the true message payload before publish

#### Bridge Sub-Option B: Opaque Tunnel

The bridge forwards browser WebSocket frames to an upstream MQTT connection with minimal protocol awareness.

Pros:

- less business logic in the bridge
- narrower application-layer surface

Cons:

- weaker authorization and rate-control options
- harder to multiplex or optimize
- still not truly blind, because the server terminates the WebSocket and can technically inspect traffic

### Recommended Bridge Choice

Use a `protocol-aware bridge` if a bridge is needed.

Reason:

- this is a multi-user product
- you need authorization, topic policy, quotas, observability, and abuse controls
- operational control matters more than pretending the bridge is blind

But the design must then explicitly address private outbound sends:

- if private sends remain plaintext JSON over MQTT, the bridge will see them
- if you want the bridge not to see private message content, the private send model must change so the browser constructs the final encrypted publish payload itself

That is a protocol/product design decision, not a transport tweak.

## Public vs Private Data Paths

This split must remain explicit.

### PublicShared

Use the backend for:

- ingest
- decode
- message history
- node projections
- channel projections
- discovery
- analytics
- API reads

The browser is a consumer of backend read models.

### SensitiveIsolated

Use the browser for:

- key entry
- key storage in memory
- decrypt
- protobuf decode
- live private rendering

The backend may know only:

- the authenticated user/workspace
- that a private feed session exists
- minimal activity metadata if explicitly designed

The backend must not store:

- raw private keys
- decrypted private payload text

## Crypto Options

### Preferred Crypto Stack

For browser-side private feeds:

- `Web Crypto API` for AES-CTR
- `Web Worker` for decrypt/decode pipeline
- either JS-based protobuf decoding or a browser-safe shared decoder layer

Sources:

- MDN Web Crypto API:
  https://developer.mozilla.org/en-US/docs/Web/API/Web_Crypto_API
- MDN AES-CTR:
  https://developer.mozilla.org/en-US/docs/Web/API/AesCtrParams

Reason:

- browser-native crypto
- secure-context aware
- available in workers
- avoids tying the first version to WebAssembly multithreading complexity

### WebAssembly Multithreading

This is an optimization option, not the starting point.

Microsoft exposes `ServeMultithreadingHeaders` for WebAssembly components, which is related to `SharedArrayBuffer` availability and cross-origin isolation behavior.

Source:

- Microsoft Learn API docs:
  https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.components.webassembly.server.webassemblycomponentsendpointoptions?view=aspnetcore-10.0

Recommendation:

- start with a standard Web Worker
- evaluate WASM multithreading only after measurement

## Major Hurdles

### 1. Upstream WSS Availability Is Not Guaranteed

This is the first real external dependency.

I did not find official confirmation that `mqtt.meshtastic.org` provides a browser-facing WebSocket endpoint.

That means:

- the browser-first private path must assume a bridge fallback

Verified example of a Meshtastic-compatible broker with WebSockets:

- `mqtt.meshnet.si` documents WebSockets on port `8884`
- Source: https://mqtt.meshnet.si/

### 2. Private Outbound Sends Need A New Security Story

Inbound private traffic is simpler because it already arrives as ciphertext from the mesh.

Outbound private sends are harder:

- current send flow builds plaintext JSON in the app and queues/publishes it
- a bridge or backend that sees that JSON sees the private message text

Therefore:

- browser-first private receive is easy
- browser-first private send without bridge visibility requires a redesigned send protocol

This must be treated as a separate implementation slice.

### 3. Auth Model Must Become API-Friendly

Today the app uses cookie auth for Blazor Server.

A browser-first frontend still can use cookie auth, but the backend must expose explicit API and bridge authorization rules.

The frontend should not depend on server-side component state for:

- workspace selection
- live subscriptions
- page refresh semantics

### 4. Current UI Is Coupled To Server-Injected Services

Today many pages inject server-side services directly.

A browser-first frontend requires:

- explicit HTTP APIs
- explicit realtime channels
- browser-side state stores

This is a structural UI refactor, not just a render-mode flip.

### 5. Public Efficiency Must Not Be Lost

The browser-first plan does not mean every user should subscribe to public MQTT feeds directly.

That would recreate the same duplication problem we are trying to remove.

Public feeds must remain backend-shared.

## Recommended Product Shape

### Frontend

Long-term:

- browser-first client project for user-facing pages
- minimal or no user-facing Blazor Server circuit dependence

Transition:

- keep auth shell and some administrative pages in the current host while migrating

### Backend API

Add explicit APIs for:

- public message pages
- public node pages
- channel summaries
- topic discovery
- profile and preset management
- workspace settings

### Realtime

Public:

- backend push via SignalR or equivalent grouped feed updates

Private:

- browser-side MQTT over WSS

### Worker

Keep:

- shared public ingest
- public projections
- durable public history

Remove over time:

- private-key ownership
- private content decryption

## Migration Plan

### Phase 1: Prepare Browser-First Foundations

Tasks:

- create `MeshBoard.Web.Client`
- enable global or route-level `Interactive WebAssembly`
- stop building new user-facing features on Interactive Server-only assumptions
- introduce explicit HTTP APIs for public read models

Acceptance:

- at least one major page works entirely from API data in the browser

### Phase 2: Public UI Migration

Tasks:

- move `Home`, `Messages`, `Nodes`, `Map`, and `Topics` to client-backed pages
- keep backend as API + push source
- reduce direct page dependence on server-side injected application services

Acceptance:

- user-facing public pages no longer require server-side circuit state for core interaction

### Phase 3: Private Receive Path

Tasks:

- add browser-side private key prompt
- add browser-side MQTT over WSS client
- add client-side decrypt/decode worker
- add private live stream page

Acceptance:

- private key never reaches backend persistence
- decrypted private messages render only in browser

### Phase 4: Bridge Fallback

Tasks:

- add protocol-aware MQTT WebSocket bridge if upstream WSS is unavailable
- enforce auth, workspace policy, quotas, and topic filtering

Acceptance:

- browser clients can use private live feeds even if the upstream broker lacks `wss://`

### Phase 5: Private Send Redesign

Tasks:

- redesign the private send path so confidentiality goals are explicit
- choose whether the bridge may see plaintext sends or whether the browser must construct final encrypted payloads

Acceptance:

- private send behavior matches a documented confidentiality guarantee

### Phase 6: Retire User-Facing Blazor Server Dependence

Tasks:

- move remaining user-facing pages off Interactive Server
- keep server-side interactivity only where justified

Acceptance:

- server circuit count is no longer proportional to normal active user load

## Recommended First Implementation Slice

If you choose this direction, the right first step is:

1. create the `.Client` project
2. enable browser-first public page execution for one route
3. expose explicit API endpoints for that route's data
4. do not touch private crypto yet

Why:

- it proves the frontend mode change
- it reduces Blazor Server dependence immediately
- it does not force the private-crypto design to be solved in the same slice

## Decision Summary

This browser-first direction is valid and likely better for your scale target than staying primarily Blazor Server.

Recommended stance:

- `Browser-first frontend`: yes
- `Backend-free product`: no
- `Public feeds directly in each browser`: no
- `Private feeds directly in the browser`: yes
- `Protocol-aware MQTT bridge fallback`: yes

That gives you:

- lower server UI load
- preserved public ingest efficiency
- stronger private-channel secrecy
- a coherent long-term architecture instead of accumulating more Blazor Server-specific complexity
