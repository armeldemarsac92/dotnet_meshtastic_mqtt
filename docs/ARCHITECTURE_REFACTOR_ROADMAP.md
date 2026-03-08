# Architecture Refactor Roadmap

This document is the implementation roadmap for turning MeshBoard into a high-scale multi-user web application.

It replaces the earlier ambiguous operating-model discussion. The operating model is now fixed.

## Accepted Product Direction

MeshBoard is intended to be:

- a web application
- used by many concurrent users
- safe for multi-user isolation
- scalable beyond a single local operator instance

This means the current architecture is transitional, not the target.

## Why The Current Architecture Will Not Scale

Current effective flow:

`MQTT callback -> decode -> SQLite transaction -> Blazor pages poll SQLite`

Current structural problems:

- a singleton MQTT runtime is shared across the whole app
- server switching is global to the process
- user data is not partitioned by user or workspace
- SQLite is used as settings store, event history, projection store, and live query store
- the UI polls for updates instead of receiving push invalidations
- several screens over-fetch and aggregate in memory
- the ingest callback is tightly coupled to persistence latency

For a many-user web app, these are not just optimizations to consider. They are architectural blockers.

## Target Architecture

Preferred target architecture:

`Browser -> Web App/API -> PostgreSQL`

`Browser <- SignalR / push invalidation <- Web App/API`

`MQTT -> Ingestion Worker -> bounded queue -> decode/project -> PostgreSQL`

`Web App/API <-> Redis or equivalent cache/pubsub (recommended)`

### Logical responsibilities

#### Web App/API

Owns:

- authentication and authorization
- user/workspace-aware commands
- read APIs and Blazor UI
- push notifications to connected clients
- cache-backed query access where appropriate

#### Ingestion Worker

Owns:

- MQTT connections
- subscription management for tracked brokers/channels
- packet decoding
- message deduplication
- projection updates
- backpressure and burst handling

#### Database

Owns:

- tenant/workspace-scoped configuration data
- message history
- node projections
- channel projections
- durable broker/workspace configuration

#### Cache / PubSub

Recommended for:

- query result caching
- realtime invalidation fan-out
- horizontal scale-out coordination

## Non-Negotiable Design Principles

Future agents should treat these as hard constraints.

- No app-wide mutable state for user-visible runtime choices.
- No singleton MQTT session shared across all users.
- No hidden write side effects inside read APIs.
- No broad data fetches in Razor just to compute screen-specific summaries.
- No dependence on high-frequency polling for core live UX.
- No assumption that SQLite remains the production primary store.

## Roadmap Overview

The work should be executed in phases. Do not start with UI polish. Start by fixing ownership, isolation, and throughput boundaries.

## Phase 0: Record The Decision And Baseline The System

Objective:

- record the accepted operating model
- measure the current system before structural changes

Tasks:

- add an ADR recording the accepted multi-user direction
- document expected scale assumptions:
  - concurrent users
  - target message ingest rate
  - expected retention size
  - number of managed brokers or workspaces
- add instrumentation for:
  - inbound MQTT messages per second
  - decode duration
  - DB write duration
  - page query latency
  - polling query frequency
  - number of active circuits
- add a repeatable synthetic ingest/load harness
- remove side-effectful default-profile creation from read paths

Suggested code areas:

- `src/MeshBoard.Application/Services/BrokerServerProfileService.cs`
- `src/MeshBoard.Application/Services/MeshtasticIngestionService.cs`
- `src/MeshBoard.Infrastructure.Meshtastic/Hosted/`
- `tests/`

Acceptance criteria:

- ADR exists and is accepted
- baseline measurements are documented
- `GetActiveServerProfile()` no longer mutates persistence during reads

## Phase 1: Introduce User And Workspace Boundaries

Objective:

- make configuration and user state safe for many-user use

Current problem:

- favorites, topic presets, active server selection, and runtime state are effectively global

Tasks:

- choose the isolation model:
  - per user
  - per team/workspace
  - per organization + workspace
- add the required identifiers to contracts and persistence schema
- scope these entities explicitly:
  - broker server profiles
  - topic presets
  - favorites
  - active subscription intents
  - user preferences
- define ownership rules for shared message visibility:
  - global shared feed
  - workspace-specific feed views
  - user-private settings on top of shared telemetry
- update repository interfaces and SQL to require scope identifiers
- add authorization checks at the application boundary

Recommended new concepts:

- `WorkspaceId`
- `UserId`
- `WorkspaceMembership`
- `SubscriptionIntent`

Acceptance criteria:

- no user preference or active selection is process-global
- persistence tables reflect explicit isolation boundaries
- tests cover cross-workspace isolation

## Phase 2: Split Runtime Ownership From The Web Process

Objective:

- stop treating the Blazor web app as the owner of MQTT ingestion runtime

Current problem:

- web UI lifecycle and ingestion lifecycle are coupled
- runtime state is implicitly embedded in the same app process

Target design:

- the ingestion worker owns MQTT runtime
- the web app owns user interactions and queries

Tasks:

- move MQTT hosted logic into a dedicated worker service or at minimum a separately bounded runtime module
- define commands from web layer to runtime layer, for example:
  - connect workspace broker
  - disconnect workspace broker
  - update subscription intents
  - switch active broker profile for a workspace
- define durable subscription intent storage
- have the worker reconcile actual MQTT subscriptions from persisted intent state
- introduce a runtime status model that the web app reads, not owns

Suggested components:

- `MeshBoard.Worker.Ingestion`
- `IBrokerRuntimeRegistry`
- `IWorkspaceBrokerSessionManager`
- `IBrokerIntentRepository`

Acceptance criteria:

- the web app can restart without becoming the authoritative owner of ingestion state
- MQTT connection ownership is not tied to a web circuit or UI scope
- runtime state can be queried independently from UI requests

## Phase 3: Migrate Production Persistence To PostgreSQL

Objective:

- replace SQLite as the production primary store

Why now:

- many-user concurrency and continuous ingestion will hit SQLite limits early
- PostgreSQL is a more appropriate fit for concurrent readers and writers

Tasks:

- introduce PostgreSQL persistence implementation
- keep persistence abstractions in `Application`
- separate SQL by provider cleanly
- migrate these domains first:
  - broker/workspace configuration
  - favorites and presets
  - message history
  - node projections
  - discovered topics or channel projections
- decide whether SQLite remains for:
  - tests only
  - local development only
  - unsupported legacy mode
- add migration scripts and rollback notes

Possible structure:

- `MeshBoard.Infrastructure.Persistence.Postgres`
- `MeshBoard.Infrastructure.Persistence.Sqlite` for dev/test only

Acceptance criteria:

- PostgreSQL is the default production target
- concurrent read/write load can be tested without file-locking semantics dominating behavior
- integration tests run against the new provider

## Phase 4: Add A Bounded Ingest Pipeline

Objective:

- decouple MQTT callbacks from decode and persistence work

Current problem:

- receive callback latency is coupled directly to decode and DB latency

Target design:

`MQTT receive -> bounded queue -> decode worker -> projection writer`

Tasks:

- add a bounded channel or queue for inbound MQTT messages
- enqueue quickly from the MQTT callback
- add decode workers and projection workers
- define overload behavior explicitly:
  - backpressure
  - drop policy
  - dead-letter logging
  - alarms
- add queue metrics:
  - depth
  - oldest message age
  - dropped count
  - worker throughput
- batch projection writes where safe
- document ordering guarantees or lack thereof

Acceptance criteria:

- MQTT callback work is minimal
- burst traffic does not immediately create callback stalls
- overload behavior is measurable and test-covered

## Phase 5: Build Proper Read Models For Screens

Objective:

- align data access with actual UI use cases

Current problem:

- UI pages fetch general lists and derive specific views in memory

Tasks:

- add query-specific repository and service methods:
  - `GetNodeById`
  - `GetMessagesPage`
  - `GetMessagesByChannel`
  - `GetChannelSummary`
  - `GetTopNodesByChannel`
  - `GetLocatedNodes`
  - `GetFavoriteNodeIdsByWorkspace`
- move filtering, sorting, and aggregation into SQL
- introduce projection tables if needed:
  - `channel_activity`
  - `node_activity_summary`
  - `workspace_runtime_status`
- add indexes based on real query patterns
- reserve full-text search decisions for a dedicated search phase if needed

Acceptance criteria:

- major screens no longer load oversized result sets and filter in Razor
- DB query count and transferred rows per screen drop materially
- screen latency remains stable as message history grows

## Phase 6: Replace Polling With Push-First Realtime Delivery

Objective:

- remove page-level periodic polling as the primary live-update mechanism

Current problem:

- load scales with active tabs and circuits instead of with new events

Target design:

- projection writers emit domain-level change notifications
- the web app pushes targeted invalidations or deltas through SignalR or equivalent

Tasks:

- introduce a projection change notifier abstraction
- publish events after successful commits:
  - message added
  - node updated
  - channel summary updated
  - runtime status changed
- use SignalR or equivalent push mechanism for connected clients
- keep manual refresh and low-frequency fallback refresh only as resilience paths
- ensure push is workspace-aware and authorization-safe

Acceptance criteria:

- dashboard and message stream no longer depend on aggressive polling
- DB query load is driven mostly by user actions and real events
- users only receive updates they are authorized to see

## Phase 7: Add Caching And Scale-Out Coordination

Objective:

- reduce database pressure and prepare for horizontal scale

Tasks:

- add cache-aside or query-result caching for hot read paths
- use Redis or equivalent for:
  - push invalidation coordination across nodes
  - shared cache
  - optional distributed locking if truly needed
- add runtime-safe cache invalidation rules
- define what data may be cached and for how long
- benchmark single-node and multi-node behavior

Acceptance criteria:

- hot dashboards and common queries avoid unnecessary DB round-trips
- multiple web instances can serve consistent realtime updates

## Phase 8: Hardening, Security, And Operational Readiness

Objective:

- make the system safe to operate as a real multi-user product

Tasks:

- add authentication and authorization coverage if not already present
- protect cross-workspace data paths with integration tests
- add rate limiting for expensive endpoints and send operations
- add structured audit logs for configuration changes
- define SLOs and alerts:
  - ingest lag
  - queue saturation
  - projection lag
  - SignalR delivery issues
  - DB latency
- document deployment topology and operational playbooks

Acceptance criteria:

- the system has explicit operational safeguards
- failure modes are observable
- cross-tenant data leakage is tested against

## Recommended Execution Order

Execute in this order:

1. Phase 0
2. Phase 1
3. Phase 2
4. Phase 3
5. Phase 4
6. Phase 5
7. Phase 6
8. Phase 7
9. Phase 8

Rationale:

- many-user support fails first on isolation and runtime ownership, not on UI polish
- storage migration should happen before investing deeply in SQLite-specific optimizations
- push and caching should be built on top of explicit read/write boundaries, not before

## Highest-Value Initial Tickets

### Ticket 1

Title:

- Make broker profile reads side-effect free and record the accepted operating model

Deliverables:

- ADR committed
- startup-only seeding
- integration tests for non-mutating reads

### Ticket 2

Title:

- Introduce workspace-scoped configuration and favorites

Deliverables:

- schema changes
- repository signature changes
- workspace isolation tests

### Ticket 3

Title:

- Extract MQTT runtime into an ingestion worker boundary

Deliverables:

- worker project or clear runtime module boundary
- durable subscription intent model
- runtime status query path

### Ticket 4

Title:

- Move production persistence to PostgreSQL

Deliverables:

- provider implementation
- migrations
- integration tests against PostgreSQL

### Ticket 5

Title:

- Add bounded ingest queue and worker pipeline

Deliverables:

- queue abstraction
- metrics
- overload handling tests

### Ticket 6

Title:

- Replace screen over-fetching with query-specific read services

Deliverables:

- direct `GetNodeById`
- direct `GetMessagesByChannel`
- `GetLocatedNodes`
- screen migrations

### Ticket 7

Title:

- Replace polling-based live updates with SignalR invalidations

Deliverables:

- projection notifier
- authorized push delivery
- reduced polling strategy

## Required Test Themes

Every phase must add tests. At minimum, cover:

- workspace isolation
- authorization boundaries
- server/runtime switching correctness
- duplicate packet handling
- decoded packet promotion
- queue overload behavior
- projection consistency under batching
- read-model query correctness
- push notification authorization and targeting

## Future Agent Working Format

Future agents should create or update a work log entry using this template whenever they implement a roadmap item.

```md
# Refactor Work Log

## Metadata

- Date:
- Agent:
- Roadmap phase:
- Ticket:
- Workspace model assumption: multi-user-web

## Objective

One short paragraph describing the exact outcome targeted in this iteration.

## Scope

- In scope:
- Out of scope:

## Files Expected To Change

- `path/to/file`

## Data Boundary Impact

- Affected scopes:
- New identifiers introduced:
- Authorization impact:

## Runtime Impact

- Does this change MQTT runtime ownership?
- Does this affect queueing or projection flow?
- Does this affect push notifications?

## Implementation Plan

1. Step one
2. Step two
3. Step three

## Acceptance Criteria

- [ ] Criterion 1
- [ ] Criterion 2
- [ ] Criterion 3

## Validation Plan

- Automated tests:
- Manual checks:
- Load or performance checks:
- Security checks:

## Risks

- Risk:
- Mitigation:

## Result

- Completed:
- Not completed:
- Follow-up needed:

## Evidence

- Commands run:
- Metrics before:
- Metrics after:
- Relevant test results:
```

## Agent Rules For This Roadmap

Future agents should follow these rules:

- do not add new global mutable runtime state
- do not keep user-scoped settings in process-global singletons
- do not use SQLite-specific constraints as the basis for long-term production design
- do not add more polling loops unless there is a documented fallback reason
- do not keep screen-specific aggregation in Razor if SQL or projections can own it
- preserve dedupe semantics unless intentionally redesigned and re-tested
- update this roadmap when a phase is materially completed, split, or reprioritized

## Definition Of Done

This roadmap is complete only when all of the following are true:

- the system is explicitly multi-user and isolation-safe
- MQTT runtime ownership is outside transient web scopes
- production persistence is server-grade
- ingest is buffered and observable
- major screens use query-shaped reads
- live updates are push-first
- the system can scale horizontally with clear operational visibility
