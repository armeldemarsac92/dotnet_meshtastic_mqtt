# Collector Event-Driven Pipeline Plan

- Status: Proposed
- Date: 2026-03-26
- Scope: Refactor the current collector into an event-driven collector pipeline with dedicated ingress, normalization, stats projection, and graph projection workers.

## Purpose

This document defines the target execution plan for moving the current collector from an in-process `MQTT -> decode -> persist` flow into a Kafka-backed pipeline that:

- keeps worker responsibilities narrow
- scales ingress, normalization, stats writes, and graph writes independently
- preserves the browser trust boundary for user Meshtastic keys
- keeps the current PostgreSQL collector model working while adding the `timescaledb` extension
- introduces Neo4j as a dedicated topology read model
- keeps the public collector API stable during migration

This plan is intentionally written in the style already used by this repository:

- small projects with explicit responsibilities
- contracts and queue messages in `MeshBoard.Contracts`
- thin worker boundaries
- service layer orchestration
- repository-driven persistence
- Dapper/SQL for PostgreSQL-side writes

HTTP consumption rules from `docs/SDK_ASSEMBLY_CONSUMPTION_GUIDE.md` remain mandatory when any worker needs to call `MeshBoard.Api`.

## Inputs And Local Constraints

This plan is constrained by the existing architecture and documentation:

- `docs/AGENT_CSHARP_STYLE.md`
- `docs/SDK_ASSEMBLY_CONSUMPTION_GUIDE.md`
- `docs/PROJECT_FOUNDATIONS.md`
- `docs/COLLECTOR_POSTGRES_SCHEMA.md`
- `docs/ARCHITECTURE_REFACTOR_ROADMAP.md`

The current extraction seams already exist:

- `src/MeshBoard.Collector/Program.cs`
- `src/MeshBoard.Infrastructure.Meshtastic/Hosted/MeshtasticInboundProcessingHostedService.cs`
- `src/MeshBoard.Application/Services/MeshtasticIngestionService.cs`

Those files show the current monolithic collector flow and should be treated as the main source to split rather than rewrite from scratch.

## Resolved Design Decisions

The following decisions are fixed for this plan unless a later ADR changes them:

- The collector pipeline is internal and Kafka-backed.
- The first four deployable collector roles are:
  - `collector-ingress`
  - `collector-normalizer`
  - `collector-stats-projector`
  - `collector-graph-projector`
- The stats store stays on the current PostgreSQL setup for now, but the collector database must become Timescale-enabled.
- Neo4j is added as the dedicated topology graph store.
- The collector does not archive raw messages in a product-owned database.
- Kafka retention is an operational replay window, not product message history.
- The stats and graph projectors are independent workers with separate consumer groups.
- The normalizer decrypts only collector-owned traffic. It must not use browser-only user vault keys.

## Non-Negotiable Constraints

- Browser-only user Meshtastic keys remain browser-only.
- Meshtastic user keys must never be stored in PostgreSQL, Neo4j, Kafka, or worker configuration.
- Product persistence and collector persistence remain explicitly separated.
- `MeshBoard.Api` remains thin and does not absorb collector worker logic.
- PostgreSQL writes remain Dapper/SQL-driven.
- Queue and event contracts live in `MeshBoard.Contracts`, not in worker projects.
- Worker consumers stay thin and call services.
- Repository SQL stays out of worker entrypoints and service constructors.
- Workspace scoping must remain explicit in every event and persistence model even if the current collector runtime still uses the default workspace.

## Target Runtime Topology

The target runtime topology is:

```text
source MQTT broker(s)
    ->
collector-ingress
    ->
Kafka topic: collector.raw-packets.v1
    ->
collector-normalizer consumer group
    ->
Kafka topic(s):
    collector.packet-normalized.v1
    collector.node-observed.v1
    collector.link-observed.v1
    collector.telemetry-observed.v1
    collector.dead-letter.v1
    ->
collector-stats-projector consumer group
    -> PostgreSQL + Timescale extension

collector-graph-projector consumer group
    -> Neo4j
```

This topology deliberately keeps the first stage minimal. The ingress worker subscribes to MQTT and publishes immutable raw events only. It does not parse, decrypt, or persist.

## Worker Model

### 1. Collector Ingress

Responsibility:

- subscribe to the upstream MQTT broker(s)
- capture broker metadata and receipt timestamps
- publish raw packet events to Kafka

Does not own:

- decryption
- topic parsing beyond what is required to describe the inbound event
- PostgreSQL writes
- Neo4j writes

### 2. Collector Normalizer

Responsibility:

- consume raw packet events
- resolve collector channel identity
- decrypt collector-owned traffic when applicable
- parse Meshtastic payloads using shared decoding logic
- classify event shape
- publish canonical normalized events

Does not own:

- PostgreSQL schema updates
- Neo4j graph writes
- public collector API reads

### 3. Collector Stats Projector

Responsibility:

- consume normalized packet and observation events
- update PostgreSQL current-state tables
- update rollups and later Timescale hypertables / continuous aggregates
- remain the owner of the relational collector model

Does not own:

- graph analytics
- Neo4j writes
- MQTT subscriptions

### 4. Collector Graph Projector

Responsibility:

- consume normalized node and link observation events
- upsert Neo4j nodes and relationships
- update topology-local counters and last-seen metadata

Does not own:

- collector relational rollups
- MQTT subscriptions
- browser-local decrypt concerns

### 5. Optional Later Worker: Graph Analytics

This is not part of the first mandatory four-worker split, but it should be planned explicitly.

Reason:

- connected components
- articulation points
- community detection
- precomputed topology badges

should not be recomputed on every packet mutation inside the graph projector hot path.

Phase 1 may compute those values at query time in Neo4j if load is acceptable.

Phase 2 should move whole-graph analytics into:

- a scheduled hosted service inside `collector-graph-projector`, or
- a dedicated `collector-graph-analytics` worker

The hot-path projector should only apply local graph mutations.

## Project Structure Proposal

The new project split should follow the style guidance in `docs/AGENT_CSHARP_STYLE.md`.

Recommended new projects:

- `src/MeshBoard.Collector.Ingress`
  - worker host
  - MassTransit registration
  - MQTT subscription bootstrap
- `src/MeshBoard.Collector.Normalizer`
  - worker host
  - raw packet consumers
  - normalized event producers
- `src/MeshBoard.Collector.StatsProjector`
  - worker host
  - PostgreSQL projection consumers
- `src/MeshBoard.Collector.GraphProjector`
  - worker host
  - Neo4j projection consumers
- `src/MeshBoard.Infrastructure.Eventing`
  - MassTransit + Kafka rider registration
  - common topic naming
  - common headers / correlation helpers
- `src/MeshBoard.Infrastructure.Graph`
  - Neo4j repositories
  - Cypher command builders
  - graph mapping helpers

Existing shared projects remain the home for reusable logic:

- `src/MeshBoard.Contracts`
  - all queue and event contracts
  - event headers and observability helpers
  - collector event version constants
- `src/MeshBoard.Application`
  - orchestration services
  - projectable domain services
  - normalization and projection use-case services
- `src/MeshBoard.Infrastructure.Meshtastic`
  - MQTT runtime
  - Meshtastic decoding
  - extraction seam for shared decode helpers
- `src/MeshBoard.Infrastructure.Persistence`
  - PostgreSQL repositories
  - SQL query classes
  - Timescale migration and schema bootstrap changes

## Implementation Conventions By Layer

These rules are binding for the worker implementation:

- `Program.cs` files stay bootstrap-only.
- Consumer classes are transport adapters only.
- Services own orchestration, idempotency flow, and queue publishing decisions.
- PostgreSQL repositories stay Dapper-based and thin.
- SQL lives in dedicated query classes.
- Mapping between queue contracts and persistence models lives in static mapping extensions.
- Configuration and DI registration live in extension classes under `DependencyInjection/`.
- If a worker consumes HTTP APIs, it must use `MeshBoard.Api.SDK` and a local wrapper service.

### Suggested Per-Worker Layout

Example worker layout:

```text
src/MeshBoard.Collector.Normalizer/
  Consumers/
  Services/
  DependencyInjection/
  Configuration/
  Program.cs
```

Example shared contracts layout:

```text
src/MeshBoard.Contracts/
  CollectorEvents/
    RawPackets/
    Normalized/
    Headers/
    TopicNames/
    Mapping/
```

## Topic Model

The initial Kafka topic model should be:

- `collector.raw-packets.v1`
  - source-of-truth replay surface for ingress-to-normalizer
  - immutable raw packets only
  - short retention
- `collector.packet-normalized.v1`
  - one normalized packet event per successfully parsed packet
  - contains normalized identity, classification, and shared metadata
- `collector.node-observed.v1`
  - emitted when a node current-state mutation is observed
- `collector.link-observed.v1`
  - emitted when a canonical radio link mutation is observed
- `collector.telemetry-observed.v1`
  - emitted for telemetry values relevant to stats rollups
- `collector.dead-letter.v1`
  - poison events and non-recoverable decode failures

This topic split is intentionally conservative.

Do not start with dozens of tiny topics.

The first slice should keep the event model simple enough that:

- the normalizer publishes a small number of canonical event types
- the stats projector subscribes only to the topics it actually needs
- the graph projector subscribes only to the topics it actually needs

## Consumer Groups

Required consumer groups:

- `meshboard-collector-normalizer`
- `meshboard-collector-stats-projector`
- `meshboard-collector-graph-projector`

Rules:

- all normalizer instances use the same consumer group on `collector.raw-packets.v1`
- all stats projector instances use the same consumer group on normalized topics
- all graph projector instances use the same consumer group on normalized topics
- the stats projector and graph projector must not share a consumer group

## Partition Strategy

The initial partition key must prefer ordering by channel-local topology over global ordering.

Recommended first key:

- `workspaceId|brokerServer|topicPattern`

Why:

- Meshtastic traffic is naturally channel-scoped
- node and link updates are most often queried by server/channel
- this preserves relative ordering for packet-derived state inside the same mesh segment

Do not partition by random GUID.

Do not partition by packet ID alone.

Those would destroy useful locality for projection.

## Retention And Replay Policy

Because the collector does not own raw message history, retention must be operational, not product-facing.

Initial policy:

- `collector.raw-packets.v1`
  - short retention only
  - enough for replay, debugging, and backfill after worker incidents
- normalized topics
  - longer retention than raw
  - sufficient to rebuild one or both projections without MQTT re-capture
- dead-letter topic
  - retained long enough for operator inspection

This plan does not require a message archive table in PostgreSQL.

## Event Contract Proposal

### RawPacketReceived

This contract is published only by `collector-ingress`.

Required fields:

- `EventId`
- `SchemaVersion`
- `WorkspaceId`
- `BrokerServer`
- `Topic`
- `PayloadBase64` or binary payload
- `ReceivedAtUtc`
- `CollectorInstanceId`
- `CorrelationId`
- `TraceParent`

Rules:

- immutable after publish
- no parsed fields
- no decrypted plaintext
- no user key references

### PacketNormalized

This contract is published only by `collector-normalizer`.

Required fields:

- `EventId`
- `SchemaVersion`
- `WorkspaceId`
- `BrokerServer`
- `Topic`
- `TopicPattern`
- `Region`
- `ChannelName`
- `MeshVersion`
- `ReceivedAtUtc`
- `ObservedAtUtc`
- `PacketId`
- `PacketType`
- `FromNodeId`
- `ToNodeId`
- `GatewayNodeId`
- `PayloadPreview`
- `DecodeStatus`
- `DecryptStatus`
- `IsPrivate`
- `CorrelationId`
- `TraceParent`

Optional fields:

- node metadata
- telemetry metrics
- traceroute hops
- normalized neighbor list

### NodeObserved

Required fields:

- `EventId`
- `SchemaVersion`
- `WorkspaceId`
- `BrokerServer`
- `TopicPattern`
- `NodeId`
- `ObservedAtUtc`
- `ShortName`
- `LongName`
- `Latitude`
- `Longitude`
- `BatteryLevelPercent`
- `Voltage`
- `ChannelUtilization`
- `AirUtilTx`
- `UptimeSeconds`
- `TemperatureCelsius`
- `RelativeHumidity`
- `BarometricPressure`
- `LastHeardChannel`
- `IsTextMessage`

### LinkObserved

Required fields:

- `EventId`
- `SchemaVersion`
- `WorkspaceId`
- `BrokerServer`
- `TopicPattern`
- `ChannelKey`
- `SourceNodeId`
- `TargetNodeId`
- `ObservedAtUtc`
- `SnrDb`
- `LinkOrigin`

`LinkOrigin` examples:

- `neighborinfo`
- `meshpacket`
- `traceroute`

### TelemetryObserved

Required fields:

- `EventId`
- `SchemaVersion`
- `WorkspaceId`
- `BrokerServer`
- `TopicPattern`
- `NodeId`
- `ObservedAtUtc`
- `MetricType`
- `MetricValue`

This contract is intentionally generic. It is the future seam for richer time-series analytics without proliferating one-off telemetry tables too early.

## Idempotency Rules

The full pipeline must assume at-least-once delivery.

That means:

- every published event must carry a stable event identity
- every PostgreSQL projector write must be idempotent
- every Neo4j projector write must be idempotent
- consumer retry must never produce duplicate logical state

### Postgres Projector Idempotency

The first phase should reuse current upsert semantics where possible:

- `collector_nodes`
- `collector_neighbor_links`
- hourly rollups

Do not move business SQL into consumers.

Instead:

- consume event
- map to SQL request DTO
- call projector service
- service coordinates repositories and transaction
- repository uses existing `INSERT ... ON CONFLICT ... DO UPDATE` patterns

### Neo4j Projector Idempotency

Use deterministic graph keys:

- node key: `workspaceId + brokerServer + nodeId`
- channel key: `workspaceId + brokerServer + topicPattern`
- link key: `workspaceId + brokerServer + channelKey + canonicalSourceNodeId + canonicalTargetNodeId`

Graph writes must use `MERGE`-style semantics, not blind creates.

The graph projector should update:

- `lastSeenAtUtc`
- observation counters
- latest SNR
- stable channel / broker properties

The graph projector should not recompute whole-graph analytics per event.

## PostgreSQL And Timescale Plan

The first storage phase must preserve the current relational schema behavior.

Phase 1:

- move the collector database to a Timescale-enabled PostgreSQL image
- enable `CREATE EXTENSION IF NOT EXISTS timescaledb`
- keep current tables and public API behavior
- keep existing repositories and query contracts working

Phase 2:

- add new collector observation tables as hypertables
- back the existing hourly rollup semantics with continuous aggregates where it adds value
- keep the current public DTO shape stable while the storage mechanics evolve

Important rule:

Installing the Timescale extension alone is not the goal. The point is to preserve the current PostgreSQL setup first, then create an opt-in evolution path for time-series-heavy collector data.

## Neo4j Graph Model Proposal

The first graph model should stay minimal.

Required node labels:

- `CollectorWorkspace`
- `BrokerServer`
- `CollectorChannel`
- `MeshNode`

Required relationship types:

- `(:CollectorWorkspace)-[:HAS_SERVER]->(:BrokerServer)`
- `(:BrokerServer)-[:HAS_CHANNEL]->(:CollectorChannel)`
- `(:MeshNode)-[:OBSERVED_ON]->(:CollectorChannel)`
- `(:MeshNode)-[:RADIO_LINK]->(:MeshNode)`

`RADIO_LINK` relationship properties:

- `workspaceId`
- `brokerServer`
- `channelKey`
- `topicPattern`
- `observationCount`
- `lastSeenAtUtc`
- `lastSnrDb`
- `maxSnrDb`
- `linkOrigins`

`MeshNode` properties:

- `nodeId`
- `shortName`
- `longName`
- `lastHeardAtUtc`
- `lastKnownLatitude`
- `lastKnownLongitude`
- `batteryLevelPercent`
- `voltage`
- `channelUtilization`
- `airUtilTx`
- `uptimeSeconds`

Deferred properties:

- `componentId`
- `communityId`
- `bridgeNode`
- `degree`
- `layoutX`
- `layoutY`

Those may be added by the later graph analytics pass rather than the hot-path graph projector.

## Worker Responsibilities In Repo Style

### Collector Ingress

Implementation shape:

- Consumer surface: none
- Hosted service: MQTT subscription loop
- Service layer: `IRawPacketPublisherService`
- Event producer: Kafka topic producer

Rules:

- no decode logic
- no persistence logic
- no Neo4j dependency
- no PostgreSQL dependency

### Collector Normalizer

Implementation shape:

- MassTransit consumer for `RawPacketReceived`
- service layer for channel resolution, decode orchestration, and normalized event publishing
- reuse extracted Meshtastic decoding helpers from `MeshBoard.Infrastructure.Meshtastic`

Extraction candidates from current code:

- `IMeshtasticEnvelopeReader`
- topic/channel parsing helpers
- `MeshtasticInboundProcessingHostedService` queue/worker semantics

Rules:

- keep consumers transport-thin
- keep decryption/normalization in services
- publish canonical domain events only after successful parsing
- route non-recoverable decode failures to a dead-letter path with structured reason codes

### Collector Stats Projector

Implementation shape:

- consumers for normalized packet and observation events
- projector service per aggregate or feature
- repositories and SQL in `MeshBoard.Infrastructure.Persistence`

Extraction candidates from current code:

- `MeshtasticIngestionService`
- existing node upsert repositories
- existing neighbor link repositories
- existing topic discovery services

The first projector implementation should wrap current repository logic rather than replacing it.

### Collector Graph Projector

Implementation shape:

- consumers for node and link observation events
- graph projector service
- Neo4j repository layer
- Cypher command builders or static query classes

Rules:

- do not embed Cypher in consumer handlers
- do not perform graph-wide analytical recomputation per inbound event
- keep writes bounded to affected node / channel / relationship scope

## Phase Plan

### Phase 0: ADR And Contracts Freeze

Goals:

- record the collector event-driven direction
- freeze worker boundaries
- freeze the first topic model
- freeze the first event contract set

Deliverables:

- ADR for collector server-side decryption scope
- ADR for Kafka-backed internal collector pipeline
- contract changelog entry
- local compose impact review

Acceptance criteria:

- no unresolved ambiguity remains on whether the normalizer may use browser-only keys
- no unresolved ambiguity remains on whether raw messages are archived

### Phase 1: Shared Contracts And Eventing Infrastructure

Goals:

- add collector event contracts to `MeshBoard.Contracts`
- add topic naming constants
- add event observability helpers
- add eventing DI abstractions

Deliverables:

- `CollectorEvents` contracts namespace
- topic naming class
- headers/correlation helpers
- MassTransit/Kafka DI registration extensions

Acceptance criteria:

- worker projects can reference contracts without depending on each other
- no worker-local duplicate queue message types exist

### Phase 2: Extract Decode And Channel Resolution Seams

Goals:

- isolate shared logic from the current collector host
- make decode/normalize reusable outside the current monolith

Tasks:

- extract channel resolution logic into reusable service seams
- extract normalized-envelope mapping into services independent from PostgreSQL write flow
- remove hard dependency between decode and `MeshtasticIngestionService`

Acceptance criteria:

- a normalization service can produce normalized event contracts without writing PostgreSQL

### Phase 3: Build Collector Ingress Worker

Goals:

- subscribe to MQTT
- publish raw packet events

Tasks:

- create `MeshBoard.Collector.Ingress`
- register MQTT runtime
- publish `RawPacketReceived`
- add health checks and ingress lag metrics

Acceptance criteria:

- raw topic receives live traffic
- no persistence occurs in ingress worker

### Phase 4: Build Collector Normalizer Worker

Goals:

- consume raw packets
- decode and normalize
- publish normalized domain events

Tasks:

- create `MeshBoard.Collector.Normalizer`
- add raw packet consumer
- call shared normalization service
- publish normalized topics
- add dead-letter handling

Acceptance criteria:

- normalized events are published for known packet types
- non-decodable packets are classified, not silently dropped
- duplicate normalization due to retry does not break downstream idempotency

### Phase 5: Build Stats Projector Against Current PostgreSQL Model

Goals:

- keep current collector relational behavior
- move it behind a dedicated projector worker

Tasks:

- create `MeshBoard.Collector.StatsProjector`
- wrap current relational ingestion logic in projector services
- reuse existing repositories and SQL
- move transaction ownership into projector services

Acceptance criteria:

- current public collector API returns the same results from projector-fed PostgreSQL data
- stats projector can be replayed from normalized topics after a database reset

### Phase 6: Enable Timescale On Collector PostgreSQL

Goals:

- keep current PostgreSQL model intact
- open the path to better time-series storage

Tasks:

- switch collector database runtime to Timescale-enabled PostgreSQL
- add extension bootstrap migration
- add no-op validation that the extension is present

Acceptance criteria:

- existing collector queries still work
- migrations apply successfully
- no public API contract changes are required

### Phase 7: Introduce Optional Hypertables And Continuous Aggregates

Goals:

- preserve API shape while improving stats storage ergonomics

Tasks:

- define observation hypertables
- backfill from normalized topics or current rollups if needed
- add continuous aggregates where they replace manual rollup maintenance cleanly

Acceptance criteria:

- storage evolution remains internal
- API contracts stay stable
- rollback to current rollup writes remains possible

### Phase 8: Build Graph Projector

Goals:

- persist topology state in Neo4j
- move topology read pressure off the current in-memory analysis path

Tasks:

- create `MeshBoard.Collector.GraphProjector`
- define Neo4j repositories
- consume node/link observation events
- upsert nodes, channels, and radio links

Acceptance criteria:

- graph can be rebuilt from normalized topics
- repeated event replay does not create duplicate graph structures

### Phase 9: Add Neo4j Read Seam

Goals:

- prepare API cutover without breaking the existing client

Tasks:

- add graph read service abstractions
- add topology query adapter that can be switched behind the API
- preserve current response DTOs

Acceptance criteria:

- topology API can be implemented against Neo4j without changing the client payload shape

### Phase 10: Topology Analytics Refinement

Goals:

- remove graph-wide analysis from the current application hot path

Tasks:

- decide between query-time Neo4j analytics and scheduled derived-property writes
- add layout precomputation if needed
- precompute bridge-node and connected-component metadata when beneficial

Acceptance criteria:

- topology API no longer needs to load all current links into application memory to compute graph metrics

## Testing Plan

### Unit Tests

- event contract mapping
- topic naming
- partition key generation
- idempotency key generation
- normalizer classification behavior
- graph key canonicalization

### Integration Tests

- ingress publishes raw events
- normalizer consumes raw and emits normalized events
- stats projector writes current PostgreSQL tables correctly
- graph projector writes Neo4j state correctly
- replaying the same normalized events does not duplicate state

### End-To-End Tests

- MQTT fixture input -> raw topic -> normalized topics -> PostgreSQL/Neo4j
- API topology endpoint still returns valid topology payloads after graph cutover
- stats endpoints still return bounded rollup views after projector cutover

### Load Tests

- raw packet ingress throughput
- normalizer consumer lag under burst traffic
- stats projector write latency under duplicate event replay
- graph projector write latency under dense link traffic

## Observability Plan

Every worker must emit structured logs and metrics.

Required metrics:

- ingress received rate
- raw topic publish failures
- consumer lag by topic and group
- decode success rate
- decrypt failure rate
- dead-letter rate
- PostgreSQL projector transaction latency
- Neo4j projector write latency
- replay duration by projection

Required logs:

- intent logs before consuming or projecting
- success logs with event identifiers
- warning logs for recoverable decode or persistence misses
- error logs with correlation identifiers and sink identifiers

## Rollout Plan

The rollout order should be:

1. contracts and eventing base
2. ingress worker
3. normalizer worker
4. stats projector using the current PostgreSQL model
5. Timescale enablement
6. graph projector
7. API read seam
8. graph analytics refinement

Do not cut over topology reads to Neo4j before:

- graph replay is proven
- graph idempotency is proven
- API parity is measured

## Rollback Plan

Rollback must remain possible per stage.

Examples:

- if ingress publish fails, revert traffic back to the current direct collector path
- if normalizer is unstable, keep raw topic publishing behind a feature flag and leave current collector persistence active
- if stats projector diverges, switch the public collector read path back to current PostgreSQL-fed logic
- if Neo4j projection is unstable, keep topology backed by the current application-side analysis until graph parity is proven

## Definition Of Done

This workstream is not done unless:

- the four primary workers exist and run independently
- contracts are versioned and documented
- the stats projector can rebuild PostgreSQL state from normalized topics
- the graph projector can rebuild Neo4j state from normalized topics
- the public collector API remains stable during rollout
- the browser-only trust boundary for user keys is preserved
- load and replay behavior are measured, not assumed

## Open Decisions

The following decisions still require explicit resolution during implementation:

- exact Kafka broker target for local and production environments
- whether the first normalized event surface is one topic or several
- whether the graph analytics pass is query-time or scheduled
- when to move from current rollup tables to Timescale hypertables / continuous aggregates
- whether the topology API should cut over all at once or behind a per-endpoint feature flag
