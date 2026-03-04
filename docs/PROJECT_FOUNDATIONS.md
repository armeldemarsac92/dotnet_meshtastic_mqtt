# Project Foundations

This document is the working architecture and delivery reference for this repository.

It exists so future agents can start coding without having to rediscover the core constraints, external facts, and design rules for the project.

Date of latest verification: 2026-03-04

## Project Goal

Build a Blazor application that connects to the Meshtastic MQTT broker at `mqtt.meshtastic.org` and allows the user to:

- choose one or more Meshtastic MQTT topics
- inspect discovered nodes
- inspect live messages
- mark nodes as favorites
- persist user preferences locally
- send public and private messages when the connected Meshtastic setup supports downlink

## Verified External Facts

These items were verified against official sources and should be treated as current as of 2026-03-04.

### .NET

- The latest LTS version of .NET is `.NET 10`.
- The older `blazorserver` template is no longer the template to start from. New Blazor apps should be created with the `blazor` template and configured with the appropriate render mode.
- For this project, the correct default is a Blazor Web App using `Interactive Server` render mode.

### Local Environment

- The local machine now has `.NET SDK 10.0.103` installed.
- The solution and active projects target `net10.0`.

### Meshtastic MQTT

- Meshtastic MQTT traffic is organized under topics such as `msh/<region>/2/...`.
- The `e` topic contains protobuf `ServiceEnvelope` payloads and is the canonical read path for live traffic.
- The JSON topic is available for MQTT integration scenarios, but the app should not be built around JSON as the primary read model.
- The public Meshtastic MQTT server is read-focused and has important operational constraints.
- The official Meshtastic public broker defaults are username `meshdev` and password `large4cats`.
- Sending through Meshtastic MQTT is tied to a configured gateway node and downlink support, not just raw arbitrary MQTT publishing from a UI.
- Duplicate publications are possible because multiple gateways may forward traffic.

## Core Architectural Position

This app should follow the repository C# style guide in `AGENT_CSHARP_STYLE.md`, adapted to a Blazor application.

The important translation is:

- Blazor pages and components are the boundary layer.
- Application services own orchestration and business rules.
- Repositories own persistence.
- Infrastructure adapters own MQTT and Meshtastic protocol integration.

Do not build this as a component-centric app where Razor files directly manage MQTT, SQL, or protocol logic.

## Frontend Styling

The frontend styling direction is Tailwind CSS v4.

Rules:

- Prefer utility classes directly in Razor markup over hand-written component CSS.
- Keep custom CSS limited to Tailwind input, theme tokens, and rare framework-level exceptions.
- Use Tailwind's CSS-first configuration style, not legacy heavy config-first setup.
- The Tailwind input file lives at `src/MeshBoard.Web/Styles/app.css`.
- The compiled stylesheet is written to `src/MeshBoard.Web/wwwroot/app.css`.
- The root `package.json` owns the Tailwind scripts.

Build commands:

- `npm run tailwind:build`
- `npm run tailwind:watch`

When changing the UI, prefer editing Tailwind utility classes in `.razor` files instead of introducing new `.razor.css` files unless there is a specific reason.

## Primary Architecture Rules

### Layering

The app should be structured around these layers:

1. UI boundary
2. Application services
3. Repository abstractions
4. Infrastructure implementations
5. SQL query definitions

Expected request and event flow:

`Razor component -> application service -> repository abstraction -> infrastructure repository -> SQL`

Realtime ingest flow:

`MQTT hosted service -> Meshtastic adapter -> application service -> persistence/state update -> UI state notification`

### Dependency Inversion

Dependency inversion is required.

High-level rules:

- `Web` depends on `Application` and `Contracts`, never on concrete database code.
- `Application` depends on repository and integration abstractions, never on Dapper, SQLite, MQTTnet, or protobuf transport details.
- Infrastructure projects depend on `Application` and `Contracts` to implement abstractions.
- Database choice must remain replaceable.

Concrete implications:

- Repository interfaces must live outside the persistence implementation project.
- MQTT client abstractions must live outside the MQTT implementation project.
- Application services must accept interfaces such as `INodeRepository`, `IMessageRepository`, `IFavoriteNodeRepository`, `IMqttConnection`, `IMeshtasticEnvelopeReader`, and `IUnitOfWork`.
- The first persistence implementation can be SQLite plus Dapper, but switching to PostgreSQL or another SQL engine should require replacing infrastructure registrations, SQL dialect details, and possibly query classes, not rewriting UI or service logic.

### Thin UI Rule

Blazor components should only do these things:

- collect user input
- call an application service
- render state
- subscribe to app-level state notifications

Blazor components must not:

- open MQTT connections
- parse Meshtastic protobuf payloads
- execute SQL
- enforce business rules beyond UI validation

### Service Layer Rule

Application services are the main orchestration layer and should own:

- topic subscription rules
- node discovery and update rules
- deduplication decisions
- message normalization
- favorite management rules
- write transaction boundaries
- send-message preconditions
- structured logging

### Persistence Rule

Persistence should follow the style guide:

- Dapper-based
- explicit SQL classes
- explicit request and response DTOs
- mapping in static extension methods
- unit-of-work abstraction for write flows

Do not hide persistence behind active record patterns or EF-style entity tracking.

## Proposed Solution Structure

This is the recommended starting solution layout.

```text
dotnet_meshtastic_mqtt.sln
src/
  MeshBoard.Contracts/
  MeshBoard.Application/
  MeshBoard.Web/
  MeshBoard.Infrastructure.Meshtastic/
  MeshBoard.Infrastructure.Persistence/
tests/
  MeshBoard.UnitTests/
  MeshBoard.IntegrationTests/
```

### Project Responsibilities

#### `MeshBoard.Contracts`

Contains:

- request and response DTOs
- shared models
- config classes
- enums
- typed exceptions
- mapping extensions that are truly shared

Examples:

- `BrokerOptions`
- `TopicPreset`
- `NodeSummary`
- `MessageSummary`
- `FavoriteNode`
- `NotFoundException`
- `ConflictException`

#### `MeshBoard.Application`

Contains:

- service interfaces and implementations
- repository abstractions
- integration abstractions
- application-level state abstractions
- use-case orchestration

Examples:

- `INodeService` and `NodeService`
- `IMessageStreamService` and `MessageStreamService`
- `ITopicSubscriptionService`
- `IMessageComposerService`
- `INodeRepository`
- `IMessageRepository`
- `ITopicPresetRepository`
- `IMeshtasticEventPublisher`
- `IMqttSession`
- `IUnitOfWork`

This project is where dependency inversion is enforced.

#### `MeshBoard.Web`

Contains:

- Blazor UI
- layout and pages
- component-level state containers where needed
- startup/bootstrap code
- dependency injection extensions
- hosted service registration

This project should depend on abstractions and orchestrate composition root behavior only.

#### `MeshBoard.Infrastructure.Meshtastic`

Contains:

- MQTTnet-based broker connection implementation
- topic building helpers
- protobuf envelope decoding
- Meshtastic packet normalization
- outgoing command builders for supported send flows

This project should implement application abstractions, not invent a second business layer.

#### `MeshBoard.Infrastructure.Persistence`

Contains:

- Dapper context
- repository implementations
- SQL request and response DTOs
- SQL query classes
- transaction and connection management

The first database target should be SQLite, but that choice must stay local to this project and DI wiring.

Current implementation status:

- SQLite plus Dapper is implemented.
- Schema initialization runs through a hosted service at app startup.
- Repository implementations for favorites, topic presets, nodes, and message history are now database-backed.
- The current schema creates `favorite_nodes`, `topic_presets`, `nodes`, and `message_history`.
- `message_history` now stores `packet_type` and `message_key` in addition to the original message fields.
- Topic preset seeding currently inserts `US Public Feed` and `EU Public Feed` if they do not already exist.
- With the current `Data Source=meshboard.db` connection string, the database file is created relative to the web app working directory. When launched with `dotnet run --project src/MeshBoard.Web/MeshBoard.Web.csproj`, that currently resolves to `src/MeshBoard.Web/meshboard.db`.
- SQLite startup now includes an additive migration path for `message_history`, so existing local databases upgrade in place instead of requiring deletion.

### MQTT Runtime Status

Current implementation status:

- MQTTnet-based connectivity is implemented behind the `IMqttSession` abstraction.
- The app now authenticates successfully against `mqtt.meshtastic.org:1883` using the documented Meshtastic public broker credentials.
- Default broker settings are currently stored in `src/MeshBoard.Web/appsettings.json`.
- Broker status now surfaces the latest connection status message to the UI so connect and auth failures are visible without reading server logs.
- Meshtastic protobuf decoding is now implemented inside `MeshBoard.Infrastructure.Meshtastic` using a minimal compiled schema subset.
- The current decoder supports:
  - Meshtastic `ServiceEnvelope` parsing
  - direct `MeshPacket` fallback parsing for broker payloads that are not wrapped the same way
  - node sender extraction from packet metadata and MQTT topic suffixes
  - text message payload previews
  - node-info payload decoding into short name and long name
  - position payload decoding into latitude and longitude
  - telemetry payload decoding for device metrics and environment metrics
- Encrypted or otherwise non-decoded packets are still persisted as opaque payload previews so the message stream remains complete.
- Message ingestion now computes a stable message key and uses it for repository-level deduplication, which prevents repeated broker packets from flooding local history.
- The Meshtastic MQTT hosted service now delays connect and subscribe until `ApplicationStarted`, which avoids racing persistence initialization during host startup.
- Live verification against the public broker has already produced decoded node identities such as `Meshtastic Salz (Salz)` and `Russell WSGP797 (Ru97)` in SQLite.
- The `/messages` page now supports practical client-side filtering by visibility, packet type, and free-text search over recent persisted traffic.
- The `nodes` table now stores a small telemetry set: battery level, voltage, channel utilization, air util TX, uptime, temperature, humidity, and barometric pressure.
- `/nodes` now renders position plus the currently persisted device and environment telemetry fields.
- Node querying now supports application-layer filtering and sorting by search text, location presence, telemetry presence, and sort mode.
- The `/nodes` page exposes those filters directly in the UI and uses the application service query path instead of embedding filter rules only in Razor.
- The `/nodes` page now also supports in-place favorite toggle actions and a favorites-only view backed by the existing favorite-node service and repository flow.
- The `/favorites` page is now actionable: favorite nodes can be removed directly from the table while preserving service-layer transaction and exception behavior.
- Live startup and migration for telemetry were verified against the public broker. Decoder correctness for telemetry payloads is covered by unit tests because a fresh public-broker telemetry sample was not guaranteed during the short validation window.

## Initial Functional Slices

### Slice 1: Read-Only Monitoring

Goal:

- connect to `mqtt.meshtastic.org`
- subscribe to user-selected topics
- decode Meshtastic envelopes
- show nodes and messages live

Included features:

- broker status
- topic preset selection
- live message stream
- node discovery
- node detail summary

### Slice 2: Persistence

Goal:

- keep local user-specific state and useful history

Included features:

- favorite nodes
- saved topic presets
- recent messages
- last-known node state

### Slice 3: Sending

Goal:

- support send flows only when the Meshtastic setup supports downlink through a configured gateway node

Included features:

- public text message send
- private text message send
- send precondition checks
- response and failure reporting

### Slice 4: Hardening

Included features:

- reconnect strategy
- deduplication strategy
- retention policies
- filters
- diagnostics
- richer telemetry display

## Initial Pages

Recommended routes:

- `/`
- `/topics`
- `/messages`
- `/nodes`
- `/favorites`
- `/compose`
- `/settings`

Expected responsibilities:

- `/`: connection status, throughput, recent activity
- `/topics`: choose, validate, and save subscriptions
- `/messages`: live stream with filtering
- `/nodes`: discovered nodes and details
- `/favorites`: quick access to important nodes
- `/compose`: public and private send flows when enabled
- `/settings`: broker settings, storage settings, retention, diagnostics

## Data And Persistence Plan

Use SQLite first because it is simple for local application storage.

The app must not depend on SQLite-specific behavior outside the persistence project.

Recommended initial tables:

- `topic_presets`
- `favorite_nodes`
- `nodes`
- `message_history`
- `processed_packets`

### Why `processed_packets` matters

Meshtastic MQTT traffic can contain duplicates.

The system needs an idempotency mechanism so repeated broker publications do not create duplicate message history rows or repeat node state transitions unnecessarily.

## Meshtastic Integration Notes

### Read Path

Read from Meshtastic protobuf topics and normalize the data into application events.

The protocol adapter should expose typed application models such as:

- `TextMessageReceived`
- `TelemetryReceived`
- `PositionReceived`
- `NodeInfoReceived`

Do not leak raw MQTT payloads into the UI layer.

### Send Path

Sending should be treated as conditional capability.

The UI must not imply that message send is always available. The application should expose a capability check based on configuration and gateway support.

The first send implementation should prefer the documented Meshtastic MQTT path that works with a configured gateway node, not an invented raw packet format.

## Non-Goals For First Coding Pass

Do not start with:

- offline-first sync
- multi-user auth
- large plugin systems
- CQRS or MediatR
- EF Core
- component-driven business logic
- direct broker logic inside Razor files

## Initial Test Strategy

### Unit Tests

Add tests for:

- topic parsing
- Meshtastic envelope normalization
- deduplication rules
- service-layer decision logic
- mapper correctness

### Integration Tests

Add tests for:

- repository behavior
- SQL mappings
- transaction handling
- app service flows against the persistence implementation

## Delivery Order

When coding begins, the preferred order is:

1. Install `.NET 10` locally.
2. Create the solution and projects.
3. Add shared contracts and application abstractions.
4. Add persistence abstractions and SQLite Dapper implementation.
5. Add MQTT and Meshtastic infrastructure adapter.
6. Add read-only vertical slice.
7. Add persistence-backed favorites and topic presets.
8. Add send capability only after the read path is stable.
9. Add tests for normalization, repository behavior, and service rules.

## Rules For Future Agents

- Follow `AGENT_CSHARP_STYLE.md` as the coding style baseline.
- Preserve dependency inversion. Do not let UI or application code depend on concrete persistence or MQTT libraries.
- Keep repository interfaces out of the infrastructure implementation project.
- Keep Blazor components thin.
- Keep SQL explicit and local to query classes.
- Keep mapping explicit.
- Prefer pragmatic layering over framework-heavy abstractions.
- Before implementing sending, verify the current Meshtastic MQTT send path and public-broker constraints again from official docs.
- If a future agent changes the architecture materially, update this document in the same change.

## Known Open Questions

- Which Meshtastic region and topic presets should be the first-class defaults in the UI?
- Should message history be persisted indefinitely, or should it use retention windows by count or age?
- Should the node list store only the latest known state, or also a history of node state changes?
- How much send capability should be exposed when the app is connected only to the public broker without a user-controlled gateway node?

## Official Sources

- .NET support policy: https://dotnet.microsoft.com/en-us/platform/support/policy
- .NET SDK templates: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-new-sdk-templates
- ASP.NET Core Blazor overview: https://learn.microsoft.com/en-us/aspnet/core/blazor/
- ASP.NET Core Blazor render modes: https://learn.microsoft.com/en-us/aspnet/core/blazor/components/render-modes
- Meshtastic MQTT integration: https://meshtastic.org/docs/software/integrations/mqtt/
- Meshtastic public MQTT connection notes: https://meshtastic.org/docs/software/integrations/mqtt/connect-to-public-server/
- Meshtastic MQTT module configuration: https://meshtastic.org/docs/configuration/module/mqtt/
- Meshtastic protobuf definitions: https://github.com/meshtastic/protobufs
