# Plan: Unify Repository Assembly Topology

- Status: Proposed
- Date: 2026-03-27
- Scope: Repository assembly graph, solution structure, and internal folder topology

## Purpose

The repository already has a mostly sane dependency graph, but it does not follow one single structural topology end to end.

Today the codebase uses three patterns at once:

- layered core and infrastructure assemblies
- feature-oriented folders in `Api`, `Contracts`, and parts of `Application`
- worker-style executable projects in the `MeshBoard.Collector.*` family

This plan defines a canonical topology and an execution sequence to move the repo toward it without forcing a risky one-shot reorganization.

The goal is not to collapse everything into one assembly model. The goal is to make the existing models explicit, consistent, and easy to reason about.

## Current State

### Current Assembly Graph

At the dependency level, the graph is mostly clean:

```text
MeshBoard.Contracts
  -> MeshBoard.Application
    -> MeshBoard.Infrastructure.Persistence
    -> MeshBoard.Infrastructure.Neo4j
    -> MeshBoard.Infrastructure.Meshtastic
  -> MeshBoard.Api.SDK
    -> MeshBoard.Client

MeshBoard.Api
  -> MeshBoard.Application
  -> MeshBoard.Contracts
  -> MeshBoard.Infrastructure.Persistence
  -> MeshBoard.Infrastructure.Neo4j

Collector executables
  -> MeshBoard.Application
  -> MeshBoard.Contracts
  -> specific infrastructure projects
```

This is good enough to preserve. The main inconsistencies are in naming, grouping, and folder layout.

### Current Solution Topology

`MeshBoard.slnx` currently groups projects only into two flat folders:

- `/src/`
- `/tests/`

That is technically valid, but it hides the actual project families:

- core
- API surface
- infrastructure
- collector pipeline
- tools and support executables
- tests

### Current Internal Folder Topology

Some assemblies already have strong internal conventions:

- `MeshBoard.Contracts`
  - bounded folders such as `Authentication`, `Collector`, `Messages`, `Nodes`, `Realtime`
- `MeshBoard.Api`
  - feature folders such as `Authentication`, `Preferences`, `Public`, `Realtime`
  - cross-cutting `Extensions` and `Middlewares`
- `MeshBoard.Infrastructure.Persistence`
  - explicit `Context`, `SQL`, `Mapping`, `Repositories`, `Migrations`, `Initialization`

Other assemblies are mixed:

- `MeshBoard.Application`
  - has `Authentication`, `Collector`, `Meshtastic`, `Observability`, `Workspaces`
  - but still keeps a broad `Services/` bucket that mixes many feature areas and a few infrastructure-shaped types
- `MeshBoard.Collector.*`
  - shares a clear family prefix
  - but each worker uses a slightly different internal folder shape

### Concrete Structural Seams

These are the main places where the topology is not unified:

1. `MeshBoard.slnx` exposes only flat `/src` and `/tests` folders, not actual project families.
2. `MeshBoard.Application` is partly feature-sliced and partly generic-service-bucket.
3. The `MeshBoard.Collector.*` family is conceptually unified but physically implemented as unrelated sibling projects.
4. The collector workers do not all use the same internal host structure.
5. A few implementation names in `Application` read infrastructure-specific, for example:
   - `src/MeshBoard.Application/Services/PostgresTopologyReadAdapter.cs`
6. Utility/support projects are not clearly separated from product runtime projects:
   - `MeshBoard.ProductMigrationTool`
   - `MeshBoard.RealtimeLoadTests`
7. Tests reference many leaf assemblies directly, which makes the topology visible but also couples tests to internal project boundaries.

## Target Topology

The target structure keeps the current layered graph but makes project families explicit.

### Canonical Project Families

The solution should present these families:

- `Core`
  - `MeshBoard.Contracts`
  - `MeshBoard.Application`
- `Api Surface`
  - `MeshBoard.Api`
  - `MeshBoard.Api.SDK`
  - `MeshBoard.Client`
- `Infrastructure`
  - `MeshBoard.Infrastructure.Persistence`
  - `MeshBoard.Infrastructure.Neo4j`
  - `MeshBoard.Infrastructure.Meshtastic`
  - `MeshBoard.Infrastructure.Eventing`
- `Collector`
  - `MeshBoard.Collector.Ingress`
  - `MeshBoard.Collector.Normalizer`
  - `MeshBoard.Collector.StatsProjector`
  - `MeshBoard.Collector.GraphProjector`
  - `MeshBoard.Collector.TopologyAnalyst`
- `Tools`
  - `MeshBoard.ProductMigrationTool`
  - `MeshBoard.RealtimeBridge`
  - `MeshBoard.RealtimeLoadTests`
- `Tests`
  - `MeshBoard.UnitTests`
  - `MeshBoard.IntegrationTests`

### Canonical Internal Assembly Rules

Libraries should prefer:

- feature folders first
- cross-cutting folders only when they are truly shared
- no generic `Services` bucket once feature ownership is clear

Hosts and worker executables should prefer:

- `Configuration/`
- `DependencyInjection/`
- `Observability/`
- exactly one of:
  - `Hosted/`
  - `Consumers/`
  - `Workers/`
- `Services/` only for host-local orchestration helpers

Infrastructure assemblies should prefer:

- adapter and repository implementations grouped by boundary
- mapping and transport/persistence shape folders explicit
- no HTTP- or UI-shaped contracts emitted directly from repositories without a mapping step

### Canonical Application Layout

The target `MeshBoard.Application` should move from:

- `Authentication/`
- `Collector/`
- `Meshtastic/`
- `Observability/`
- `Services/`
- `Workspaces/`

to a stricter feature topology such as:

- `Authentication/`
- `Collector/`
- `Favorites/`
- `Messages/`
- `Nodes/`
- `Preferences/`
- `Realtime/`
- `Topics/`
- `Workspaces/`
- `Meshtastic/`
- `Observability/`
- `Caching/`
- `Abstractions/`
- `DependencyInjection/`

The exact feature names can be adjusted, but the key rule is that each application type should belong to one bounded feature folder, not to a catch-all `Services/` namespace.

## Non-Goals

This plan does not require:

- merging assemblies
- rewriting the existing dependency graph
- changing public HTTP contracts
- changing product runtime behavior
- renaming every namespace in one batch

Those would make the migration much riskier than necessary.

## Gap Analysis Summary

| # | Gap | Severity | Notes |
|---|-----|----------|-------|
| 1 | `MeshBoard.slnx` uses only flat `/src` and `/tests` folders | MEDIUM | Hides the actual architecture |
| 2 | `MeshBoard.Application/Services` is a mixed catch-all | HIGH | Biggest internal topology inconsistency |
| 3 | `MeshBoard.Collector.*` family lacks a canonical host layout | MEDIUM | Same family, different shapes |
| 4 | Some implementation types in `Application` read infrastructure-specific | MEDIUM | Example: `PostgresTopologyReadAdapter` |
| 5 | Utility/support projects are not surfaced as a distinct family | LOW | Makes active runtime boundaries harder to read |
| 6 | Test projects reference many internal leaf assemblies directly | LOW | Useful for now, but increases coupling |

## Phase 1: Solution Topology Hardening

**Goal:** Make the assembly families visible in `MeshBoard.slnx` without changing code behavior.

**Files to modify:**

- `MeshBoard.slnx`
- `docs/PROJECT_FOUNDATIONS.md`

**Actions:**

- replace the flat `/src` folder with family-level solution folders
- group projects under `Core`, `Api Surface`, `Infrastructure`, `Collector`, `Tools`, and `Tests`
- update `PROJECT_FOUNDATIONS.md` to reflect the canonical family list

**Why first:**

- zero runtime risk
- gives the repo a visible target structure immediately
- makes later folder moves reviewable

**Acceptance criteria:**

- `MeshBoard.slnx` clearly reflects project families
- no project references change in this phase
- `dotnet build MeshBoard.slnx` passes

## Phase 2: Canonicalize Application Feature Topology

**Goal:** Remove the generic `MeshBoard.Application.Services` bucket and assign each type to a feature.

**Primary source area:**

- `src/MeshBoard.Application/Services/`

**Likely target groups:**

- `Authentication`
  - `UserAccountService`
- `Collector`
  - `CollectorReadService`
  - `TopicDiscoveryService`
- `Messages`
  - `MessageService`
  - `ChannelReadService`
- `Nodes`
  - `NodeService`
- `Preferences`
  - `BrokerServerProfileService`
  - `FavoriteNodeService`
  - `ProductBrokerPreferenceService`
- `Realtime`
  - `RealtimeSessionService`
  - `RealtimeJwksService`
  - `RealtimeTopicAccessPolicyService`
  - `RealtimeTopicFilterAuthorizationService`
  - `RealtimePacketEnvelopeFactory`
  - `RealtimePacketPublicationFactory`
  - `RealtimeSigningKeyMaterialResolver`
  - `VernemqWebhookAuthorizationService`
- `Topics`
  - `TopicExplorerService`
- `Workspaces`
  - `WorkspaceProvisioningService`
- `Meshtastic`
  - `NullTopicEncryptionKeyResolver`
  - `MeshtasticIngestionService`

**Actions:**

- move service files into owned feature folders
- update namespaces to match physical ownership
- keep interfaces with implementations where that pattern already exists
- keep `DependencyInjection` as the one cross-cutting registration folder

**Important rule:**

- do not mix mechanical moves with business-logic rewrites

**Acceptance criteria:**

- `MeshBoard.Application/Services/` no longer contains feature-owned business services
- namespaces match the feature folder layout
- `dotnet build` and unit tests pass

## Phase 3: Remove Infrastructure-Shaped Implementations From Application

**Goal:** Keep `Application` technology-agnostic in both dependency shape and naming.

**Immediate target:**

- `src/MeshBoard.Application/Services/PostgresTopologyReadAdapter.cs`

**Preferred outcomes:**

- move the implementation into `MeshBoard.Infrastructure.Persistence`
- keep only the abstraction in `MeshBoard.Application.Abstractions.Collector`
- register the infrastructure implementation from the persistence assembly

**Possible destination examples:**

- `src/MeshBoard.Infrastructure.Persistence/Adapters/TopologyReadAdapter.cs`
- `src/MeshBoard.Infrastructure.Persistence/Collector/TopologyReadAdapter.cs`

**Notes:**

- the current implementation only depends on application abstractions and contracts
- the problem is mostly topology and naming, not dependency correctness

**Acceptance criteria:**

- `Application` does not contain persistence-technology-specific adapter names
- DI registration remains explicit
- `dotnet build` passes

## Phase 4: Normalize Collector Family Topology

**Goal:** Make the `MeshBoard.Collector.*` family feel like one coherent subsystem.

**Current issue:**

- the family is consistent in naming
- it is inconsistent in internal folder shape

**Canonical collector worker template:**

- `Configuration/` when needed
- `DependencyInjection/`
- `Observability/`
- one primary execution folder:
  - `Consumers/` for message-driven workers
  - `Workers/` for scheduled/hosted workers
  - `Hosted/` for host lifecycle services
- `Services/` for worker-local orchestration only

**Examples to normalize:**

- `MeshBoard.Collector.GraphProjector`
- `MeshBoard.Collector.Normalizer`
- `MeshBoard.Collector.StatsProjector`
- `MeshBoard.Collector.Ingress`
- `MeshBoard.Collector.TopologyAnalyst`

**Resolved decision:**

- the `MeshBoard.Collector.*` worker family is the only collector surface
- the legacy root `MeshBoard.Collector` host has been retired

**Acceptance criteria:**

- collector workers use one recognizable host topology
- no legacy root `MeshBoard.Collector` executable remains in the solution
- solution folders reflect the collector family clearly

## Phase 5: Separate Tools From Product Runtime

**Goal:** Make support executables and operational utilities clearly distinct from product runtime assemblies.

**Candidate projects:**

- `MeshBoard.ProductMigrationTool`
- `MeshBoard.RealtimeLoadTests`
- `MeshBoard.RealtimeBridge`

**Recommended handling:**

- keep them as projects under `src/` for now if path churn is not worth it
- but surface them in a distinct `Tools` or `Operations` family in the solution
- document whether each project is:
  - product runtime
  - operational support
  - migration-only
  - performance/load testing

**Note:**

`MeshBoard.RealtimeBridge` is runtime-relevant, but still operationally distinct from `Api` and `Client`. It should stay visible as its own host family member.

**Acceptance criteria:**

- support projects are not visually mixed with core runtime projects in the solution
- `PROJECT_FOUNDATIONS.md` names active runtime projects separately from support tooling

## Phase 6: Test Topology Review

**Goal:** Ensure test project references reflect intended architectural seams.

**Current state:**

- unit tests reference several product assemblies directly
- integration tests reference `Api`, `Application`, `Contracts`, `Infrastructure.Persistence`, and migration tooling

**Actions:**

- review whether each test project is intentionally cross-assembly
- keep broad references where they provide value
- avoid adding new direct leaf references without a clear reason
- optionally introduce more focused test projects later if ownership boundaries become blurry

**Non-goal for now:**

- do not explode the test suite into many per-assembly test projects unless pain justifies it

**Acceptance criteria:**

- test references are intentional and documented
- no unnecessary new dependency edges are introduced during topology cleanup

## Migration Order

Recommended order:

1. Phase 1: solution topology
2. Phase 2: application feature topology
3. Phase 3: infrastructure-shaped implementation cleanup
4. Phase 4: collector family normalization
5. Phase 5: tools separation
6. Phase 6: test topology review

This order front-loads low-risk structure and postpones namespace churn and worker-family decisions until the repo shape is clearer.

## Verification

After each phase:

1. `dotnet build MeshBoard.slnx`
2. `dotnet test tests/MeshBoard.UnitTests/MeshBoard.UnitTests.csproj`
3. `dotnet test tests/MeshBoard.IntegrationTests/MeshBoard.IntegrationTests.csproj` when the local database environment is available
4. manual review of namespace, folder, and DI registration consistency

## Decision Log Needed Before Execution

Before phase 3 or 4 starts, explicitly resolve these questions:

1. ~~Should `MeshBoard.Collector` remain a first-class executable, or should the worker family become the only collector surface?~~ Resolved on 2026-04-02: the worker family is the only collector surface and the legacy host is retired.
2. Should `MeshBoard.RealtimeLoadTests` stay under `src/` as a support project, or move under a dedicated load/perf family later?
3. Is a physical folder move desired for support tools, or is solution-folder separation enough for the near term?
4. Do we want one canonical `Application` namespace root per feature now, or preserve mixed namespaces until after the collector work lands?

## Success Criteria

This plan is complete when:

- the solution exposes project families clearly
- `MeshBoard.Application` no longer uses `Services/` as the default ownership bucket
- infrastructure-specific implementation names no longer live in `Application`
- the collector family has one documented and recognizable host topology
- tooling and support executables are clearly separated from core runtime assemblies
- the build and tests remain green throughout the migration
