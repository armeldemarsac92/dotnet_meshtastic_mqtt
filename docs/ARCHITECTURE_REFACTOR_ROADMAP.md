# Architecture Refactor Roadmap

Historical note:

- `MeshBoard.Web` was removed from the active repository surface on 2026-03-21.
- The current cleanup/removal work now continues in `docs/CLIENT_FIRST_CLEANUP_PLAN.md`.
- Remaining references to `MeshBoard.Web` in this document describe the migration sequence that led to the current client/API/bridge architecture.

- Status: Proposed
- Date: 2026-03-12
- Scope: Full migration from Blazor Server to `Blazor WebAssembly + ASP.NET Core API + dedicated realtime tier`

## Purpose

This document is the execution plan for moving MeshBoard from a server-rendered, runtime-heavy web app into a client-heavy architecture that:

- scales to large numbers of concurrent clients
- keeps Meshtastic decryption keys on the client only
- moves authentication and preferences into a thin API
- moves live transport into a dedicated realtime tier
- removes Blazor Server circuit coupling from the product architecture

This plan is written for multi-agent execution. It defines the target architecture, the refactor sequence, worker ownership boundaries, acceptance criteria, and rollback points.

HTTP API consumption in this refactor must follow `docs/SDK_ASSEMBLY_CONSUMPTION_GUIDE.md`.

## Resolved Product Decisions

The following decisions are fixed as of `2026-03-12` unless a later ADR explicitly changes them:

- Browser-to-broker transport uses `MQTT 5 over WSS`, not a custom plain WebSocket protocol.
- Browser-to-API auth uses same-origin cookie auth.
- Browser-to-broker auth uses short-lived asymmetric JWT minted by the API.
- `VerneMQ` enforces broker auth and ACLs through MQTT 5 webhook hooks that call `MeshBoard.Api`, where the JWT is validated and the authorization decision is returned.
- The downstream broker must be strictly self-hosted and open-source.
- `VerneMQ` is the default downstream broker target.
- Local Meshtastic keys survive logout by default.
- Browser-local key storage must be encrypted at rest in wave 1.
- The local vault uses a dedicated client-only passphrase, not the account login password.
- Wave 1 local decrypted history starts as memory-only.
- IndexedDB-backed local history remains an optional later slice.
- Safari is not a required browser target for the first release.
- Compose is deferred, but the crypto and contract boundaries must keep future outbound secure messaging feasible.

## Non-Negotiable Constraints

These rules are binding for the migration:

- The browser is the trust boundary for Meshtastic decryption keys.
- Meshtastic decryption keys must never be stored in the database.
- Meshtastic decryption keys must never be required by the API or realtime tier.
- The API owns authentication, user/workspace preferences, and durable metadata only.
- The realtime tier owns live transport only.
- The client owns decrypted message state, node/channel projections derived from live traffic, and optional local-only history.
- Browser-local key material must survive logout unless the user explicitly wipes the local vault.
- Browser-local key persistence must be encrypted at rest in production.
- New server endpoints must remain thin and call application services rather than embedding business logic.
- Persistence must remain explicit and Dapper/SQL-driven unless an explicit later decision changes that.
- The migration must preserve workspace isolation semantics.

## Target Architecture

### Component Model

The target system is:

- `MeshBoard.Client`
  - Blazor WebAssembly
  - consumes `MeshBoard.Api.SDK`
  - realtime client over `WSS`
  - local key vault
  - browser-side decryption worker
  - local projections for messages, channels, nodes, and map state
- `MeshBoard.Api`
  - JSON API only
  - authentication
  - user/workspace preference persistence
  - optional metadata query endpoints
- `MeshBoard.Api.SDK`
  - thin Refit endpoint interfaces for `MeshBoard.Api`
  - HTTP client and handler registration for consumers
  - no business logic
- `MeshBoard.RealtimeBridge`
  - upstream connections to one or more source MQTT brokers
  - downstream fanout to internet-facing realtime infrastructure
  - no decryption
  - no plaintext persistence
- `Downstream Realtime Broker`
  - internet-facing, horizontally scalable client connection tier
  - default target: `VerneMQ`
  - transport: `MQTT 5 over WSS`
- `Database`
  - server-side relational store for auth and preferences
  - production target: PostgreSQL

### Preferred Live Transport Topology

The default plan assumes:

- source brokers are consumed by `MeshBoard.RealtimeBridge`
- the bridge republishes raw encrypted payloads and metadata to a downstream broker
- browsers connect to the downstream broker using `MQTT 5 over WSS`
- browsers decrypt locally

This is preferred over using the API as the main WebSocket fanout server because the system requirement is high concurrency.

Protocol rule:

- `plain WSS` means a custom application protocol over raw WebSocket frames
- `MQTT over WSS` means standard MQTT control packets transported inside WebSocket frames
- the default architecture uses `MQTT over WSS` because it is the standardized browser-compatible option, works with standard brokers, and avoids inventing a custom realtime protocol

### Trust Boundaries

The server may know:

- user identity
- saved broker profiles
- saved topic presets without keys
- favorites
- saved channel/filter selections
- optional non-secret topic discovery metadata

The server may not know:

- Meshtastic channel keys
- per-topic decryption key mappings
- decrypted message plaintext
- decrypted telemetry derived solely from protected traffic, unless a later encrypted-sync design is introduced

## Authentication Model

Authentication is split into two separate channels with separate credentials and separate validation rules.

### API Session Auth

The default browser auth model is:

- same-origin cookie authentication for `MeshBoard.Api`
- JSON auth endpoints, not Razor form posts
- antiforgery validation for state-changing API endpoints
- no API bearer token requirement for the browser in phase 1

This is the preferred default because the client is a browser app on the same origin as the API and does not need to keep its primary API session token in JavaScript-managed storage.

### Broker Session Auth

The default browser-to-broker auth model is:

- short-lived asymmetric JWT minted by `MeshBoard.Api`
- JWT audience scoped to the downstream realtime broker only
- `VerneMQ` webhook-based validation at the API boundary
- no reuse of the API session cookie as broker auth
- no reuse of the broker JWT as API auth

This split is required for scale and isolation:

- the API remains the identity authority
- the broker cluster can enforce connect/subscribe/publish decisions without querying the product database on every hook
- JWT signing keys remain private to the API/auth tier
- public verification keys can be rotated through JWKS

### Broker Session Auth Contract

The default realtime auth flow is:

1. Browser authenticates to `MeshBoard.Api` using the normal cookie session.
2. Browser calls `POST /api/realtime/session`.
3. API validates the authenticated user and workspace scope.
4. API mints a short-lived broker JWT signed with an asymmetric private key.
5. API returns:
   - `brokerUrl`
   - `clientId`
   - `token`
   - `expiresAtUtc`
   - optional `allowedTopicPatterns`
6. Browser connects over `WSS` using `MQTT.js`.
7. Browser sends the JWT in the MQTT `CONNECT` packet field chosen by broker configuration.
8. `VerneMQ` calls the configured MQTT 5 webhook hook on `MeshBoard.Api`.
9. The API validates the JWT, checks ACL/topic scope, and returns the broker decision.
10. Browser refreshes the broker token before expiry and reconnects with a new token.

Minimum broker JWT claim set:

- `iss`
- `aud`
- `sub`
- `jti`
- `iat`
- `nbf`
- `exp`
- `workspace_id`
- `user_id`
- `client_id`
- topic ACL or equivalent authorization claims

JWT security rules:

- asymmetric signing only by default
- explicit algorithm allowlist
- explicit `aud` validation
- explicit `iss` validation
- short TTL
- `kid` for rotation
- separate validation rules from any other JWT used in the system

## Contract Boundary Rules

The refactor introduces three contract families:

- `API contracts`
  - request/response DTOs exchanged with `MeshBoard.Api`
- `Client-local models`
  - key records
  - decrypted projections
  - local history records
  - client connection state
- `Bridge/session contracts`
  - realtime bootstrap payloads
  - broker session tokens
  - raw packet transport envelopes

Rules:

- no API contract may contain `EncryptionKeyBase64`, raw key bytes, or decrypted payload text
- no bridge/session contract may carry the browser's API session cookie
- client-local key models must never be reused as persistence DTOs
- contract changes must land before dependent client or bridge feature work
- all shared contract changes require contract tests
- HTTP API transport contracts and Refit endpoint interfaces belong in SDK assemblies, not in `MeshBoard.Client` or worker feature projects
- SDK assemblies own `HttpClient`, handlers, and DI registration; consumer wrappers own logging, caching, null-on-404 behavior, and use-case translation

Current model reshaping guidance:

- `BrokerServerProfile` becomes:
  - server-owned saved broker/profile metadata
  - client-owned local default key configuration if retained
- `TopicPreset` becomes:
  - server-owned saved preset metadata
  - client-owned local key binding if retained
- `MessageSummary`, `NodeSummary`, and `ChannelSummary` become client-derived by default unless explicitly retained as metadata-only API surfaces

## Architecture Rules During Refactor

These rules adapt the existing project principles to the new architecture:

- Client components remain thin. UI components orchestrate state and rendering only.
- Client-side business logic belongs in client feature services and projection stores, not in `.razor` page bodies.
- API endpoints remain thin. They validate, authorize, call application services, and return DTOs.
- Application services remain the orchestration layer for auth, preferences, and server-owned business rules.
- Repository interfaces stay outside persistence implementations.
- SQL remains in dedicated query classes.
- Realtime infrastructure stays out of the API project.
- Browser cryptography and local secret persistence stay out of the API and persistence projects.
- Pages must not talk to `MQTT.js`, workers, IndexedDB, or crypto APIs directly.
- Client feature services and projection stores own local orchestration.

## Data Ownership Matrix

| Data | Owner | Storage | Notes |
| --- | --- | --- | --- |
| Users | API | PostgreSQL | Reuse current auth model first |
| Workspace claim | API | auth cookie / token claims | Keep `workspace == user id` in first cut |
| Broker profiles | API | PostgreSQL | Remove secret key fields |
| Topic presets | API | PostgreSQL | Remove secret key fields |
| Favorites | API | PostgreSQL | Reuse current model |
| Saved channel selections | API | PostgreSQL | New or repurposed table |
| Realtime session token | API | ephemeral signed token | Used for broker/WSS connection |
| Decryption keys | Client | IndexedDB + Web Crypto import | Never sent to server |
| Live message stream | Client + realtime tier | memory | Raw encrypted payloads |
| Decrypted messages | Client | memory or IndexedDB | Local-only by default |
| Node/channel projections | Client | memory or IndexedDB | Built from local stream |
| Message history server-side | None by default | none | Optional future encrypted design only |
| Runtime connection metrics | Realtime tier | local metrics system | Not a core product DB concern |

## Solution Structure

The target solution layout should be:

```text
src/
  MeshBoard.Client/
  MeshBoard.Api/
  MeshBoard.Api.SDK/
  MeshBoard.RealtimeBridge/
  MeshBoard.Contracts/
  MeshBoard.Application/
  MeshBoard.Infrastructure.Persistence/
  MeshBoard.Infrastructure.Realtime/
tests/
  MeshBoard.UnitTests/
  MeshBoard.IntegrationTests/
  MeshBoard.Client.Tests/
  MeshBoard.LoadTests/
```

Notes:

- `MeshBoard.Web` is transitional and will be deleted after cutover.
- the `MeshBoard.Collector.*` worker family is now the explicit starting point for collector and traffic-history work.
- `MeshBoard.Infrastructure.Meshtastic` should be split into reusable upstream transport pieces and deprecated server-side decode/ingest pieces.
- `MeshBoard.Api.SDK` owns Refit endpoint contracts under `API/` and registration under `DI/`.

## What Gets Reused

Keep with minimal redesign:

- `MeshBoard.Contracts`
- `MeshBoard.Application` auth and preference services
- `MeshBoard.Infrastructure.Persistence`
- workspace claim resolution and password hashing
- Dapper repository pattern and SQL organization

Keep but reshape:

- broker profile contracts
- topic preset contracts
- topic discovery logic
- runtime connection abstractions

Remove or replace:

- Blazor Server pages and notifiers
- server-side decryption pipeline
- projection-change DB polling for UI refresh
- runtime command queue inside the main web app
- server-side read models derived from decrypted traffic

## Target Server Schema

### Keep

- `users`
- `favorite_nodes`
- `broker_server_profiles`
- `topic_presets`

### Add

- `saved_channel_filters`
  - `id`
  - `workspace_id`
  - `broker_profile_id`
  - `topic_filter`
  - `label`
  - `created_at_utc`
  - `updated_at_utc`
- optional `realtime_sessions` if server-side token auditing is required
- optional `discovered_topics` if the realtime tier persists metadata-only topic discovery

### Remove From Server Schema

- `broker_server_profiles.default_encryption_key_base64`
- `topic_presets.encryption_key_base64`

### Transitional Tables To Deprecate

- `message_history`
- `nodes`
- `workspace_runtime_status`
- `broker_runtime_commands`
- `projection_change_log`
- `runtime_pipeline_status`
- `subscription_intents`
  - either delete or redefine as saved user preferences only

## Phased Execution Plan

### Phase Ownership

- Phase 0 owner: `architecture-agent`
- Phase 1 owner: `auth-agent`
- Phase 2 owner: `persistence-agent`
- Phase 3 owner: `client-agent`
- Phase 4 owner: `crypto-agent`
- Phase 5 owner: `realtime-agent`
- Phase 6 owner: `client-agent`
- Phase 7 owner: `architecture-agent`
- Phase 8 owner: `migration-agent`

Final approvers for cross-cutting surfaces:

- contracts and DTOs: `contracts-agent`
- DB migrations and destructive drops: `persistence-agent`
- broker auth claims and token policy: `security-agent` or orchestrator if no dedicated security worker exists
- client key vault format: `crypto-agent`
- rollout and deletion checklist: `migration-agent`

### Phase 0: Guardrails And Spikes

#### Outcome

Lock the architecture before large code movement begins.

#### Tasks

- choose the downstream high-concurrency transport baseline
  - fixed default: clustered broker with `MQTT 5 over WSS`
- choose the production relational database
  - default: PostgreSQL
- define browser local-storage strategy
  - IndexedDB for persistence
  - encrypted-at-rest local vault
  - Web Crypto for import/unlock/runtime crypto operations
- record local decrypted history policy
  - optional in wave 1
- record whether compose/downlink is in scope for the first migration
  - fixed default: defer

#### Deliverables

- ADR for downstream realtime topology
- ADR for browser-side key handling
- ADR for server-side data retention scope
- contract family classification
- schema delta inventory
- open decisions register

#### Exit Criteria

- no unresolved decision remains on trust boundaries
- no unresolved decision remains on transport topology

### Phase 1: API Bootstrap And Contract Split

#### Outcome

A new API project exists and owns auth plus preference CRUD without secrets.

#### Tasks

- create `MeshBoard.Api`
- create `MeshBoard.Api.SDK`
- move auth boundary out of `MeshBoard.Web`
- add `HttpContext`-based workspace accessor
- expose JSON endpoints for:
  - auth
  - broker profiles
  - topic presets
  - favorites
  - saved channels
- split shared contracts into:
  - server-owned DTOs
  - client-local key models
- remove secret key fields from server request/response contracts
- move API client transport definitions into `MeshBoard.Api.SDK`, not `MeshBoard.Client`

#### API Auth Model

Phase 1 defaults:

- cookie-based browser session auth
- same-origin deployment preferred
- JSON endpoints only
- `401/403` responses for API endpoints

Required endpoints:

- `POST /api/auth/register`
- `POST /api/auth/login`
- `POST /api/auth/logout`
- `GET /api/auth/me`

Cookie policy defaults:

- `HttpOnly`
- `Secure`
- `SameSite=Lax` by default
- `SameSite=Strict` only if the login flow still works with it
- explicit expiration and renewal policy

#### Antiforgery Rules

- all mutating API endpoints require antiforgery validation when using cookie-authenticated browser requests
- the client must send the antiforgery token in a header
- antiforgery token bootstrap must be part of the initial authenticated shell or a dedicated bootstrap endpoint

Endpoints requiring antiforgery:

- `POST /api/auth/register`
- `POST /api/auth/login`
- `POST /api/auth/logout`
- all mutating preference endpoints

Endpoints not requiring antiforgery:

- `GET` query endpoints
- broker/WSS auth validation at the downstream broker

#### Workspace Isolation Invariants

- workspace id always comes from the authenticated principal, never from client body or query input
- repository calls for server-owned data must always be workspace-scoped
- uniqueness constraints must be workspace-scoped
- no endpoint may accept a foreign workspace override

Failure behavior:

- missing workspace claim => unauthorized
- cross-workspace resource lookup => consistent `404` or `403` policy, chosen once and documented

#### Endpoint Boundary Rules

- endpoint handlers validate transport shape and auth only
- application services own transactions and business rules
- repositories own persistence only
- no SQL in endpoint mapping files
- no direct `HttpContext.User` access below the boundary/accessor layer
- no broker token signing logic inside endpoint mapping bodies
- no secret-bearing logs

#### Deliverables

- working `MeshBoard.Api`
- typed endpoint inventory
- updated contracts without server-side decryption key fields

#### Exit Criteria

- browser or test client can register, login, logout, and fetch `me`
- browser or test client can CRUD preferences without hitting `MeshBoard.Web`
- no API contract carries Meshtastic decryption key material
- antiforgery validation is enforced for mutating browser endpoints
- workspace isolation invariants are covered by tests

### Phase 2: Persistence Reshape And PostgreSQL Migration

#### Outcome

The API persists only server-owned data, on the production-grade DB engine.

#### Tasks

- introduce PostgreSQL persistence implementation
- migrate schema from runtime/read-model-centric shape to preferences-centric shape
- create SQL migrations
- backfill current user preference data where possible
- remove secret key columns from persistence
- mark transitional tables as deprecated

#### Deliverables

- PostgreSQL schema
- migration scripts
- integration tests for workspace isolation on PostgreSQL

#### Schema Migration Matrix

Table actions:

- `users`
  - retain
  - migrate provider
- `favorite_nodes`
  - retain
- `broker_server_profiles`
  - retain
  - drop `default_encryption_key_base64`
- `topic_presets`
  - retain
  - drop `encryption_key_base64`
- `saved_channel_filters`
  - add
- `subscription_intents`
  - redefine as saved preferences or deprecate
- `message_history`
  - freeze writes
  - deprecate
  - drop after cutover window
- `nodes`
  - freeze writes
  - deprecate
  - drop after cutover window
- `workspace_runtime_status`
  - remove from product DB or move to bridge-only concern
- `broker_runtime_commands`
  - remove from product DB or move to bridge-only concern
- `projection_change_log`
  - remove from product path
- `runtime_pipeline_status`
  - remove from product DB or move to bridge-only concern

Migration sequence:

1. add or reshape retained tables
2. backfill non-secret metadata where needed
3. switch API reads
4. switch API writes
5. freeze obsolete writes
6. keep rollback window
7. execute destructive drops only after cutover success

#### PostgreSQL Implementation Notes

- keep repository interfaces stable
- isolate provider-specific SQL where dialect differences matter
- prefer `uuid`, `timestamptz`, `text`, and explicit indexes
- keep normalized username columns
- avoid JSON columns unless they are clearly justified
- use forward-only migrations
- maintain repeatable local integration test environments
- run provider parity tests for workspace isolation and conflict behavior

Decision rule:

- use PostgreSQL first for the new API path
- do not spend migration effort on SQLite runtime tables that are already slated for removal

#### Provisioning Rules

- provisioning may create default broker profiles and non-secret topic presets
- provisioning must not write default decryption keys
- provisioning must not initialize runtime subscriptions as hidden side effects in the new API data model

#### Exit Criteria

- auth and preference API passes integration tests on PostgreSQL
- no server table stores decryption key material
- all retained tables are workspace-aware
- migration rehearsal passes on a copy of current data
- destructive drops are still deferred until the cutover window closes

### Phase 3: Client Shell And Auth Migration

#### Outcome

A Blazor WebAssembly client exists and can replace the current shell for auth and preferences.

#### Tasks

- create `MeshBoard.Client`
- consume `MeshBoard.Api.SDK` through client-local wrapper services
- implement login/register/logout flows
- implement authenticated app shell
- migrate:
  - settings shell
  - broker profile management
  - topic presets
  - favorites
- move shared styling tokens/Tailwind setup into the client app

#### Deliverables

- running WASM shell
- auth flow against the new API
- preference pages running without Blazor Server circuits

#### Exit Criteria

- user can authenticate and manage preferences entirely through WASM + API
- `MeshBoard.Web` is no longer required for those flows

### Phase 4: Client Key Vault And Local Decryption

#### Outcome

The browser can securely persist keys and decrypt packets locally.

#### Tasks

- define client-only key model
- implement IndexedDB key vault
- implement Web Crypto import/export boundaries
- implement a decryption worker
- port or rewrite Meshtastic decryption logic for the browser runtime
- define local projection builders for:
  - messages
  - nodes
  - channels
  - map

#### Client Key Vault Modes

- `locked mode`
  - raw key records wrapped with a passphrase-derived key before IndexedDB persistence
  - unlock required after app start
- `development convenience mode`
  - allowed only for local development or temporary migration tooling
  - must not be the production default

Production default:

- implement `locked mode` in wave 1
- keys survive logout, but the vault is re-locked on logout
- wiping keys requires an explicit user action
- the local vault passphrase must not be persisted server-side
- the local vault passphrase is dedicated to the client vault and must not be derived from or automatically reused from the account login password
- accepted bootstrap exception: see `docs/adr/0002-browser-vault-kdf-bootstrap.md` for the initial standards-only PBKDF2 vault-envelope slice before the later Argon2id hardening slice

Implementation guidance:

- persist only wrapped or encrypted key blobs, never plaintext key strings
- derive the wrapping key from a user-supplied passphrase
- preferred KDF: `Argon2id`
- fallback KDF only if browser/runtime support or performance makes Argon2id non-viable, and only with an explicit ADR
- `ADR 0002` accepts a standards-only `PBKDF2-SHA-256` bootstrap for the first vault envelope while preserving `Argon2id` as the upgrade target
- import runtime keys into Web Crypto after unlock
- runtime `CryptoKey` objects should be non-extractable where feasible
- clear unlocked key material from memory on lock, logout, and hard reset flows
- run unlock, wrap, unwrap, and decrypt hot paths in a worker when practical

Key lifecycle requirements:

- add
- update
- revoke
- lock on logout
- wipe only on explicit user reset or vault deletion
- optional backup/export only if explicitly designed and approved

Storage durability steps:

- request persistent storage using `navigator.storage.persist()`
- document fallback behavior if the browser declines persistent storage
- treat browser storage eviction as a recoverable failure mode

#### Worker Contract

The decryption worker contract must be defined before implementation.

Worker input shape must include:

- raw payload bytes
- broker identifier
- source topic
- receive timestamp
- packet identity metadata if already known

Worker output shape must include:

- normalized raw-packet event
- decrypt result classification
- decoded domain event if successful
- bounded error classification if unsuccessful

Failure classes:

- no matching key
- decrypt failure
- protobuf parse failure
- unsupported `portnum`
- malformed payload

Runtime rules:

- keep crypto and protobuf parsing off the UI thread
- keep the hot path inside the worker
- define max queue depth and degradation behavior
- emit worker metrics to the client state layer
- the worker boundary must remain reusable for a future outbound compose pipeline

#### Deliverables

- local key vault module
- local decrypt pipeline
- tests using captured encrypted packets

#### Exit Criteria

- browser can decrypt known sample packets locally
- browser can restart and retain keys locally
- no API request contains raw decryption keys
- the UI thread is not responsible for the decrypt/parse hot path

## Client Projection Model

Client projection ownership is explicit and must not leak back into page-level aggregation code.

Required client stores:

- `RawPacketStore`
- `DecryptedMessageStore`
- `NodeProjectionStore`
- `ChannelProjectionStore`
- `MapProjectionStore`
- `ConnectionStateStore`

Projection rules:

- define dedupe keys explicitly
  - preferred: `broker + topic + packet id + from node`
  - fallback: stable content hash
- separate transport ordering from semantic packet time
- use bounded in-memory retention for hot views
- wave 1 history is memory-only
- local IndexedDB history is optional, deferred, and versioned separately from API contracts
- pages subscribe to projection services, not directly to workers or `MQTT.js`

### Phase 5: Realtime Tier And High-Concurrency Transport

#### Outcome

The live stream no longer depends on Blazor Server or API-hosted runtime state.

#### Tasks

- create `MeshBoard.RealtimeBridge`
- extract reusable upstream MQTT client/session logic
- remove server-side decryption and read-model persistence from the bridge
- connect the bridge to source brokers
- publish raw encrypted envelopes and metadata into the downstream realtime broker
- add API-issued short-lived session tokens for downstream client access
- implement topic/ACL authorization model
- benchmark concurrent client connections

#### Realtime Client Protocol

Phase 5 client defaults:

- use `MQTT.js` in the browser
- `MQTT 5 over WSS` only
- stable per-session `clientId`
- short-lived broker JWT minted by the API
- reconnect with refreshed broker token
- use `transformWsUrl` for reconnect-time token refresh

Explicit non-goal:

- do not introduce a custom plain-WebSocket browser protocol for the main realtime path unless a later ADR proves that standard MQTT over WSS is insufficient

Recommended defaults:

- `QoS 0` for hot live monitoring traffic
- explicit keepalive configuration
- resubscribe only to client-owned active topics after reconnect
- disable or tightly control offline queueing for monitoring paths

#### Topic And ACL Model

The topic namespace must be explicit and frozen before broad bridge/client work starts.

The model must distinguish:

- workspace-scoped control or private topics
- shared public feed topics
- admin/debug topics

ACL rules must ensure:

- the broker JWT only authorizes the minimal topic scope required
- private or workspace-scoped traffic is not leaked across users
- public feed access can be shared without exposing private scopes

#### Multi-Tab Strategy

Wave 1 default:

- ship `simple mode`
- use one live broker connection per tab
- use `BroadcastChannel` for same-origin coordination such as logout, token refresh hints, and optional cache coordination
- do not make `SharedWorker` the primary connection model

Rationale:

- one-connection-per-tab is the most supported and operationally predictable browser model
- `BroadcastChannel` is widely available and suitable for coordination
- `SharedWorker` is not Baseline and should stay an optional optimization spike, not the core architecture

Optimization trigger:

- only pursue cross-tab connection consolidation if production-like load tests show that tab-amplified broker connections are a material cost or reliability problem

#### Deliverables

- bridge service
- downstream broker configuration
- session token issuance endpoint
- load test scripts and benchmark reports

#### Exit Criteria

- browser receives live payloads over `WSS`
- live traffic path does not depend on `MeshBoard.Web`
- concurrency benchmark meets the target budget
- broker JWT validation and ACL enforcement are tested
- `VerneMQ` webhook auth and topic decisions are covered by integration tests
- reconnect with token refresh is tested

### Phase 6: Feature Migration To Local Projections

#### Outcome

The main product experience is rebuilt on the client.

#### Tasks

- migrate messages view to local stream + local filters
- migrate nodes view to local node projections
- migrate channels view to local channel projections
- migrate map to local node location projections
- migrate dashboard to API preferences + local live summaries
- keep server data fetches limited to preferences and optional metadata

#### Recommended Slice Breakdown

- `6A`
  - live message viewer
- `6B`
  - node projection and node details
- `6C`
  - channel projection and channel details
- `6D`
  - map projection
- `6E`
  - optional local history persistence

#### Deliverables

- feature parity pages in `MeshBoard.Client`
- local projection stores
- end-to-end tests for live update behavior

#### Exit Criteria

- messages, nodes, channels, and map no longer read from server-side decrypted tables
- UI refreshes come from client state, not DB polling notifiers

### Phase 7: Compose Redesign

#### Outcome

Compose is either explicitly redesigned for the new trust model or deferred.

#### Tasks

- decide whether compose may expose plaintext to the bridge
- if yes:
  - define secure send API / realtime path
  - scope audit and authorization rules
- if no:
  - define a client-side encryption/signing-compatible send design
- remove dependency on the old runtime command queue
- keep the client key-vault and worker contracts generic enough that outbound encrypt/sign flows can be added later without redesigning the storage model

#### Deliverables

- compose ADR
- implementation or explicit defer decision

#### Exit Criteria

- no legacy runtime command path remains in the active UX

### Phase 8: Cutover And Deletion

#### Outcome

The old server-rendered runtime is removed.

#### Tasks

- disable hosted MQTT/runtime services in `MeshBoard.Web`
- stop writing projection-change events for UI refresh
- remove Blazor Server-only observability and reconnect UX
- remove deprecated runtime tables when safe
- remove `MeshBoard.Web` once parity and operational confidence are reached

#### Deliverables

- cutover checklist
- removal PRs
- post-cutover runbook

#### Exit Criteria

- production traffic uses `Client + API + RealtimeBridge`
- no critical user flow depends on `MeshBoard.Web`

## Interface Freeze Points

Mandatory freezes before parallel dependent work begins:

- Freeze A: end of Phase 0
  - downstream broker choice
  - browser auth model for realtime token issuance
  - topic naming convention
  - metadata retention policy
- Freeze B: before client and bridge parallel work
  - `POST /api/realtime/session` request/response
  - broker JWT claim set
  - token TTL and renewal rules
  - broker `clientId` format
  - ACL claim format or role mapping
- Freeze C: before local projection work
  - raw packet envelope schema sent to the browser
  - local key record format
  - local projection event shape
  - node/channel/message identity keys
- Freeze D: before cutover
  - rollout flags
  - deprecation list for old tables
  - metrics and rollback thresholds

## PR Slicing Rules

- one PR type per slice:
  - contract-only
  - schema-only
  - API-only
  - client-only
  - bridge-only
  - rollout/config-only
- do not mix DB schema, shared contracts, and client consumption changes in one PR unless it is an orchestrator-approved freeze PR
- every PR must declare:
  - prerequisite slices
  - feature flag impact
  - migration compatibility impact
  - rollback mechanism

Recommended first milestone slice order:

1. contract split
2. secret-key column removal scaffolding
3. API auth shell
4. preference endpoints
5. client shell
6. broker session token contract
7. bridge auth
8. local key vault
9. live stream client

## Multi-Agent Execution Model

### Orchestrator Responsibilities

The orchestrator agent owns:

- sequencing phases
- maintaining this roadmap
- assigning workers with disjoint write sets
- resolving cross-workstream contract questions
- enforcing no-secret-on-server constraints
- blocking merges that violate ownership boundaries

Before assigning or starting any implementation slice, the orchestrator must ensure the worker has reviewed:

- `docs/PROJECT_FOUNDATIONS.md`
- `docs/adr/0001-operating-model.md`
- `docs/AGENT_CSHARP_STYLE.md`
- the relevant frozen sections of this roadmap

### Worker Ownership Boundaries

Suggested agent role names:

- Worker A = `auth-agent`
- Worker B = `persistence-agent`
- Worker C = `contracts-agent`
- Worker D = `client-agent`
- Worker E = `crypto-agent`
- Worker F = `realtime-agent`
- Worker G = `platform-agent`
- Worker H = `security-agent`

#### Worker A: API And Auth

Owns:

- `src/MeshBoard.Api`
- auth endpoint extraction
- API DI bootstrap
- API-facing workspace context accessor
- auth and session token contracts

Must not edit:

- realtime bridge transport internals
- client key vault implementation

#### Worker B: Persistence And Schema

Owns:

- `src/MeshBoard.Infrastructure.Persistence`
- migrations
- PostgreSQL implementation
- integration test data fixtures

Must not edit:

- client app pages
- bridge fanout implementation

#### Worker C: Shared Contracts And Service Refactor

Owns:

- `src/MeshBoard.Contracts`
- `src/MeshBoard.Application`
- removal of server-side secret-key assumptions
- service signature updates

Must not edit:

- client UI features
- downstream broker deployment config

#### Worker D: Client Shell And Preferences

Owns:

- `src/MeshBoard.Client`
- auth screens
- shell
- preferences pages
- client-local wrappers around `MeshBoard.Api.SDK`

Must not edit:

- server persistence internals
- bridge upstream MQTT logic

#### Worker E: Client Crypto And Local Projections

Owns:

- client key vault
- browser decryption worker
- local message/node/channel stores
- packet decode tests in browser-compatible form

Must not edit:

- API auth flow
- database migration scripts

#### Worker F: Realtime Bridge And Broker Integration

Owns:

- `src/MeshBoard.RealtimeBridge`
- extracted MQTT transport/runtime pieces
- downstream broker publishing
- load testing for concurrency

Must not edit:

- client page rendering
- API preference CRUD

#### Worker G: Platform And Broker Operations

Owns:

- downstream broker deployment configuration
- WSS termination and network policy
- JWKS publishing and broker verification wiring
- observability and load-test infrastructure

Must not edit:

- client UI features
- API business logic

#### Worker H: Security Review

Owns:

- JWT signing and rotation policy
- cookie and antiforgery review
- CSP and browser hardening review
- broker ACL review

Must not edit:

- broad feature implementation without explicit ownership transfer

### Parallelization Rules

- Only one worker owns a given project or folder at a time.
- Shared contract changes land before dependent feature work.
- Persistence migrations land before API handlers that depend on them.
- Session token contract lands before client realtime work and bridge auth work.
- Realtime transport work and client key-vault work can proceed in parallel once contracts are stable.
- Workers must not revert changes made by other workers.
- Ambiguity in token format, topic model, local history scope, or compose plaintext policy is a stop condition and must be escalated.

### Branch And Commit Policy

Implementation work must proceed branch by branch, with atomic commits and explicit ownership.

Branch rules:

- no implementation work lands directly on `main`
- one active workstream branch per owned slice
- do not mix unrelated phase work on the same branch
- branch names should identify the phase and owned surface
  - `refactor/p1-api-auth`
  - `refactor/p2-postgres-schema`
  - `refactor/p4-client-vault`
  - `refactor/p5-realtime-bridge`
- if a worker needs a dependency branch from another worker, that dependency must be named in the handoff and PR notes

Atomic commit rules:

- one concern per commit
- no commit may mix unrelated contract, schema, client, and bridge changes unless it is an orchestrator-approved freeze commit
- commits should leave the owned slice compiling when practical
- each commit message must explain what changed and why
- each commit message must mention the affected phase or workstream when relevant
- tests or validation run for that slice must be included in the commit or PR notes

Detailed commit message shape:

```text
<type>(<scope>): short imperative summary

Why:
- reason for the change

What:
- key implementation points

Validation:
- tests or checks run
```

Preferred commit scope examples:

- `feat(api-auth)`
- `refactor(contracts)`
- `feat(client-vault)`
- `feat(realtime-bridge)`
- `chore(migrations)`

### Suggested Parallel Sequence

#### Wave 1

- Worker A: API bootstrap
- Worker B: PostgreSQL foundation and schema delta
- Worker C: contract split and service refactor

#### Wave 2

- Worker D: client shell and preference screens
- Worker F: realtime bridge extraction
- Worker E: key vault spike and decryption worker

#### Wave 3

- Worker D: messages/nodes/channels UI
- Worker E: local projections
- Worker F: broker ACLs and load tests

#### Wave 4

- Worker A: compose redesign endpoints if approved
- Worker B: deprecated table removal
- Worker C: final cleanup of old abstractions

### Handoff Contract For Every Worker

Each worker handoff must include:

- what changed
- exact files changed
- assumptions made
- blockers discovered
- tests run
- follow-up work required by another worker

### Progress Update Template

Use this shape for every meaningful worker update:

```text
Workstream:
Files owned:
Completed:
In progress:
Blocked by:
Contract changes:
Tests:
Next handoff target:
```

### Blocker Template

Use this shape whenever a worker cannot proceed safely:

```text
Blocker:
Why it blocks progress:
Files or contracts affected:
Decision needed:
Recommended resolution:
Fallback if unresolved:
```

## Coordination Artifacts

The following shared artifacts must be kept current during the migration:

- this roadmap
- ADR queue
- contract changelog
- migration changelog
- packet fixture registry
- rollout checklist
- load-test scenario registry

Rule:

- no worker may change a frozen interface without updating the changelog and notifying the dependent workers named in this roadmap

### Definition Of Done For A Workstream PR

A workstream is not done unless:

- tests were added or updated
- docs were updated if contracts or architecture changed
- no server-owned model contains secret key fields
- ownership boundaries were respected
- the PR states migration impact and rollback path

## Phase Gates

### Phase 1 Gate

- auth API contract tests
- cookie/session tests
- workspace claim tests
- antiforgery tests for mutating endpoints

### Phase 2 Gate

- migration rehearsal on a copy of current data
- forward-only migration verification
- PostgreSQL integration tests
- schema diff review against removed secret fields

### Phase 3 Gate

- browser auth E2E
- API-only preference CRUD E2E
- no dependency on Razor pages for auth/preferences

### Phase 4 Gate

- browser compatibility review for IndexedDB, Web Crypto, and workers
- captured packet decryption tests
- restart persistence tests
- corrupted-key and missing-key recovery tests

### Phase 5 Gate

- JWT/JWKS broker auth test
- reconnect storm test
- token expiry and renewal test
- topic ACL isolation test
- load-test thresholds with concrete numbers

### Phase 6 Gate

- parity checklist for messages, nodes, channels, and map
- no server-side decrypted read dependency
- local projection correctness under out-of-order and duplicate packets

### Phase 8 Gate

- shadow or canary validation
- deletion dry run for deprecated tables
- rollback drill

## Verification Strategy

### Test Layers

- unit tests for application services and client projection logic
- integration tests for API + persistence
- contract tests for API DTO compatibility
- browser tests for auth, preference CRUD, and realtime message handling
- packet fixture tests for browser-side decryption
- load tests for downstream broker concurrency and reconnect behavior
- browser compatibility checks for supported browsers

### Critical Acceptance Tests

- user can authenticate and load preferences from the API
- user can save broker profiles, presets, favorites, and channels without storing keys server-side
- browser can connect to the live realtime endpoint using a short-lived token
- browser can receive raw encrypted Meshtastic payloads and decrypt them locally
- browser restart preserves local keys
- two different users cannot access each other's saved preferences or authorized realtime scopes
- production build works with the API unavailable only for live preferences calls, not for local decrypt logic
- token expiry during an active connection results in controlled reconnect
- malformed packet bursts do not freeze the UI thread
- deleting a local key makes future matching packets fail cleanly

### Load Test Gates

Before production cutover:

- benchmark concurrent client connections against the chosen downstream broker
- benchmark reconnect storms
- benchmark topic fanout under realistic subscription patterns
- measure bridge throughput from source brokers to downstream broker

## Deployment And Rollout Plan

Environment progression:

- local
- shared dev
- staging
- shadow or canary
- production

Side-by-side rollout shape:

- keep `MeshBoard.Web` active during early migration waves
- expose `MeshBoard.Client` behind a feature flag or alternate route first
- run the bridge in shadow mode before broad client cutover

Broker cutover steps:

- validate broker token auth first
- validate ACL enforcement second
- validate reconnect and expiry behavior third
- only then enable broad client access

Rollback triggers:

- auth failure rate above threshold
- reconnect failure rate above threshold
- broker auth rejection rate above threshold
- packet loss or decrypt failure rate above threshold
- unacceptable connection amplification from multi-tab behavior

Deprecation policy:

- minimum soak window before deleting transitional tables
- minimum soak window before removing `MeshBoard.Web`

## Rollback Strategy

At the end of each phase, keep the previous runtime path usable until the next phase is proven.

- Phase 1-3 rollback: keep `MeshBoard.Web` as the active shell
- Phase 4 rollback: keep local decryption behind a feature flag and continue with API-only preference flows
- Phase 5 rollback: disable new realtime path and keep old hosted runtime for internal environments only
- Phase 6 rollback: keep legacy pages available until client parity is proven
- Phase 8 rollback: do not delete the old runtime path until telemetry and user validation are clean

## Open Decisions

These should be resolved early and tracked explicitly:

- whether non-secret metadata discovery is persisted server-side

## Immediate First Slice

The first implementation slice should be intentionally narrow:

1. Add `MeshBoard.Api` with JSON auth and preference endpoints.
2. Remove server-side secret-key fields from contracts and schema.
3. Add `MeshBoard.Client` with login, broker profiles, topic presets, and favorites.
4. Add a minimal realtime session token endpoint.
5. Start `MeshBoard.RealtimeBridge` by extracting upstream MQTT connection code without decryption.

This slice gives the team a stable base for parallel work without prematurely rebuilding every feature.

## Primary References

These sources informed the plan and should remain the default references during implementation:

- Microsoft Learn: Blazor hosting models
  - https://learn.microsoft.com/en-us/aspnet/core/blazor/hosting-models?view=aspnetcore-10.0
- Microsoft Learn: Call a web API from ASP.NET Core Blazor
  - https://learn.microsoft.com/en-us/aspnet/core/blazor/call-web-api?view=aspnetcore-10.0
- Microsoft Learn: Identity and API auth guidance
  - https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-10.0
- Microsoft Learn: Antiforgery guidance
  - https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0
- Microsoft Learn: ASP.NET Core WebSockets
  - https://learn.microsoft.com/en-us/aspnet/core/fundamentals/websockets?view=aspnetcore-10.0
- Microsoft Learn: Cookie auth without ASP.NET Core Identity
  - https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie?view=aspnetcore-10.0
- Microsoft Learn: Cookie auth API endpoint behavior in ASP.NET Core 10
  - https://learn.microsoft.com/en-us/aspnet/core/breaking-changes/10/cookie-authentication-api-endpoints?view=aspnetcore-10.0
- MDN: IndexedDB
  - https://developer.mozilla.org/en-US/docs/Web/API/IndexedDB_API/Using_IndexedDB
- MDN: Web Crypto `SubtleCrypto.importKey`
  - https://developer.mozilla.org/en-US/docs/Web/API/SubtleCrypto/importKey
- MDN: Web Crypto `SubtleCrypto.wrapKey`
  - https://developer.mozilla.org/en-US/docs/Web/API/SubtleCrypto/wrapKey
- MDN: Web Crypto `SubtleCrypto.unwrapKey`
  - https://developer.mozilla.org/en-US/docs/Web/API/SubtleCrypto/unwrapKey
- MDN: `StorageManager.persist`
  - https://developer.mozilla.org/en-US/docs/Web/API/StorageManager/persist
- MDN: Web Workers
  - https://developer.mozilla.org/en-US/docs/Web/API/Web_Workers_API/Using_web_workers
- MDN: Broadcast Channel API
  - https://developer.mozilla.org/en-US/docs/Web/API/Broadcast_Channel_API
- MDN: SharedWorker
  - https://developer.mozilla.org/en-US/docs/Web/API/SharedWorker
- MQTT.js
  - https://github.com/mqttjs/MQTT.js
- VerneMQ GitHub
  - https://github.com/vernemq/vernemq
- VerneMQ site
  - https://vernemq.com/
- EMQX listener and scaling documentation
  - https://docs.emqx.com/en/emqx/latest/configuration/listener.html
  - https://docs.emqx.com/en/emqx/latest/faq/concept.html
- EMQX JWT authentication
  - https://docs.emqx.com/en/emqx/latest/access-control/authn/jwt.html
- HiveMQ listener and cluster documentation
  - https://docs.hivemq.com/hivemq/latest/user-guide/listeners.html
  - https://docs.hivemq.com/hivemq/latest/user-guide/cluster.html
- OWASP Password Storage Cheat Sheet
  - https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html
- RFC 9106: Argon2 Memory-Hard Function
  - https://datatracker.ietf.org/doc/html/rfc9106
- RFC 8725: JSON Web Token Best Current Practices
  - https://datatracker.ietf.org/doc/html/rfc8725
- RFC 7517: JSON Web Key
  - https://datatracker.ietf.org/doc/html/rfc7517
- RFC 7519: JSON Web Token
  - https://datatracker.ietf.org/doc/html/rfc7519
