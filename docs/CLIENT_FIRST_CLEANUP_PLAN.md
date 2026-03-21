# Client-First Cleanup Plan

- Status: In progress, with the legacy runtime branch removed from active code
- Date: 2026-03-21
- Scope: Remove server-rendered-era residue and make the active browser-first architecture obvious in code, tooling, and docs

## Purpose

The broad migration plan in [ARCHITECTURE_REFACTOR_ROADMAP.md](./ARCHITECTURE_REFACTOR_ROADMAP.md) established the target architecture.

This document is the narrower execution plan for the cleanup phase after the cutover work:

- remove code and tooling that still imply `MeshBoard.Web` is the active product host
- isolate or rename the pieces that are really future collector/public-map work
- stop carrying legacy runtime-command and server-side read-model abstractions through shared registrations
- make the current architecture legible to future contributors without rediscovering which path is live

## Phase 1 Progress

Completed on 2026-03-21:

- removed `src/MeshBoard.Web`
- removed `MeshBoard.Web` from `MeshBoard.slnx`
- removed the root `Dockerfile` that published `MeshBoard.Web`
- retargeted root Tailwind scripts to `MeshBoard.Client`
- rewrote `README.md` and `docs/PROJECT_FOUNDATIONS.md` around the client/API/bridge stack
- narrowed `MeshBoard.RealtimeBridge` onto `AddBridgeApplicationServices()`
- moved the former `MeshBoard.Worker.Ingestion` host onto collector-specific registrations:
  - `AddCollectorApplicationServices()`
  - `AddMeshtasticCollectorInfrastructure()`
  - `AddCollectorPersistenceInfrastructure()`
- renamed `MeshBoard.Worker.Ingestion` to `MeshBoard.Collector`
- started the normalized PostgreSQL collector schema:
  - `collector_servers`
  - `collector_channels`
  - `collector_nodes`
  - `collector_messages`
  - `collector_neighbor_links`
- removed the dead queued-runtime branch:
  - deleted `AddMeshtasticQueuedRuntimeInfrastructure()`
  - deleted the queued runtime command service and processor hosted service
  - deleted the legacy runtime persistence registrations and SQLite runtime repositories
- switched `MeshBoard.Api`, `MeshBoard.RealtimeBridge`, and `MeshBoard.Collector` defaults to PostgreSQL-backed persistence

## Current Architecture Snapshot

The active product/runtime path today is:

- `MeshBoard.Client`
- `MeshBoard.Api`
- `MeshBoard.Api.SDK`
- `MeshBoard.RealtimeBridge`
- `VerneMQ`
- PostgreSQL-backed product persistence

The active local stack in `ops/local/compose.yaml` runs:

- `meshboard-client`
- `meshboard-api`
- `meshboard-realtime-bridge`
- `vernemq`
- `postgres`

It does not run `MeshBoard.Web`.
It does not run `MeshBoard.Collector`.

## What Is Still Confusing

The remaining confusion is narrower now:

- `docs/ARCHITECTURE_REFACTOR_ROADMAP.md` still contains migration-era text written before `MeshBoard.Web` removal.
- some docs still describe SQLite or runtime-queue behavior that no longer exists in `src`.
- the collector path is explicit now and has initial read-only public-map APIs, but it still lacks rollup tables and a finalized public-history policy, so its long-term shape is only partially visible.

At the same time, not all leftover code is equal:

- `MeshBoard.Web` is transitional and removable.
- `MeshBoard.Collector` is not part of the active product path, but it is also not dead UI legacy. It is the explicit future collector/public-map pipeline and should stay isolated from product persistence.

## Keep / Remove / Refactor

### Keep

These are part of the active architecture and should remain first-class:

- `src/MeshBoard.Client`
- `src/MeshBoard.Api`
- `src/MeshBoard.Api.SDK`
- `src/MeshBoard.RealtimeBridge`
- `src/MeshBoard.Contracts`
- `src/MeshBoard.Application` auth, preference, realtime-session, and shared contract orchestration
- `src/MeshBoard.Infrastructure.Persistence` product/PostgreSQL path
- `src/MeshBoard.Infrastructure.Meshtastic` reusable MQTT/runtime transport and packet decoding pieces
- `ops/local/compose.yaml`

### Remove

These are transitional or misleading and should be deleted once the cleanup starts:

- `src/MeshBoard.Web`
- root `Dockerfile` that still publishes `MeshBoard.Web`
- root Tailwind defaults that target `MeshBoard.Web`
- README and docs sections that describe `MeshBoard.Web` as the normal way to run the app
- solution references, launch settings, and scripts whose only purpose is the old web host

### Refactor Before Keeping

These should not stay in their current shape:

- `src/MeshBoard.Collector`
- collector-facing public-map read APIs
- collector rollup/analytics schema beyond current normalized ingest tables
- migration tooling that still references old SQLite product databases

The issue is not only dead code.
The issue is that the remaining transitional surfaces still mix three different concerns:

- active product preferences/auth
- migration/backfill support for old installs
- possible future public collector / network analytics ingestion

## Target End State

After cleanup, the repository should read as:

- active product path:
  - `Client + Api + Api.SDK + RealtimeBridge`
- optional future public collector path:
  - separate, explicit, and named as such
- no source, docs, or root tooling implying that `MeshBoard.Web` is still the product host
- no broad "legacy everything" registration path shared by bridge, collector, and removed web host

## Refactoring Principles

- Delete transitional UI and docs aggressively once parity is already achieved.
- Do not delete future collector capabilities just because they are not in the active compose stack today.
- Split by runtime role, not by historical convenience.
- Prefer narrower DI entrypoints over one large compatibility registration method.
- Product persistence and collector persistence must be separate concepts.
- If a contract is only kept alive for no-op compatibility, plan to remove it.

## Phase Plan

### Phase 1: Remove `MeshBoard.Web` As A First-Class Product Surface

Outcome:

- the repo no longer suggests that `MeshBoard.Web` is how MeshBoard runs

Tasks:

- remove `src/MeshBoard.Web` from `MeshBoard.slnx`
- delete `src/MeshBoard.Web`
- replace the root `Dockerfile` with one of these:
  - remove it entirely and make compose the primary local entrypoint
  - or repurpose it to build the client/API stack explicitly
- make the client Tailwind build the default in `package.json`
- rewrite `README.md` run instructions around:
  - `MeshBoard.Client`
  - `MeshBoard.Api`
  - `ops/local/compose.yaml`
- rewrite or retire stale sections in `docs/PROJECT_FOUNDATIONS.md`

Acceptance criteria:

- no root build/run command points at `MeshBoard.Web`
- no active solution project points at `MeshBoard.Web`
- no documentation presents Blazor Server as the current product architecture

### Phase 2: Split Runtime Roles In DI

Outcome:

- bridge, product API, and collector each register only what they actually use

Tasks:

- replace `AddApplicationServices()` with narrower role-based entrypoints
- keep `AddApiApplicationServices()` for API-only concerns
- add a bridge-focused application registration that does not register:
  - node/message read services
  - message retention
  - runtime-command query services
  - other Web-era read-model services
- add a collector-focused application registration only if collector work remains in scope
- stop using "catch-all" registrations in `MeshBoard.RealtimeBridge` and any future collector host

Acceptance criteria:

- `MeshBoard.RealtimeBridge` only references services needed for upstream consume + downstream republish
- no active runtime uses a large compatibility service bundle by default

Progress:

- complete for `MeshBoard.RealtimeBridge`
- complete for `MeshBoard.Collector`
- the broad `AddApplicationServices()` compatibility entrypoint has been removed

### Phase 3: Split Product Persistence From Collector Persistence

Outcome:

- the persistence layer makes a clean distinction between product metadata and traffic/collector storage

Tasks:

- keep `AddProductPersistenceInfrastructure()` as the API/bridge product path
- keep `AddCollectorPersistenceInfrastructure()` as the collector path
- remove no-op compatibility requirements from shared repository contracts where possible
- stop treating SQLite legacy runtime persistence as the default generic persistence path
- decide explicitly whether a collector schema is in scope now

Acceptance criteria:

- product hosts do not depend on legacy SQLite runtime tables
- the meaning of each persistence registration is obvious from its name

Progress:

- naming split is complete
- collector hosts no longer call the generic mixed persistence registration
- normalized collector ingest tables are in place
- initial read-only public collector APIs are in place
- initial hourly packet rollups are in place
- longer-term history policy is still pending

### Phase 4: Narrow `MeshBoard.Collector`

Outcome:

- the future public-map collector path is explicit, PostgreSQL-backed, and no longer confused with the removed server-rendered host

Tasks:

- keep `MeshBoard.Collector` out of the default local product compose stack unless intentionally enabled
- finish removing remaining SQLite assumptions from its runtime, tests, and docs
- decide whether collector-owned read models should stay query-compatible with product contracts or split into collector-only APIs
- define retention/aggregation policy for public map history

Acceptance criteria:

- there is no ambiguous project whose name and registrations suggest both "legacy worker" and "future collector"
- the collector path is clearly Postgres-backed and modeled around normalized server/channel ownership
- initial read-only collector queries do not depend on product-only repository contracts

### Phase 5: Remove Legacy Runtime-Command And Server Read-Model Surfaces

Outcome:

- the codebase no longer carries Blazor Server runtime management concepts that the browser-first product does not use

Candidate removal set:

- `subscription_intents`
- `broker_runtime_commands`
- `workspace_runtime_status`
- `runtime_pipeline_status`
- runtime command query services
- server-side reconnect/runtime-status surfaces
- server-side message/node read models if the collector path does not require them

Important note:

- `nodes` and `neighbor_links` are not automatically "legacy".
- If the public collector/public map stays in scope, they should be redefined as collector/public-analytics tables, not deleted blindly.
- `message_history` should not survive by default unless there is a concrete collector feature that justifies it.

Acceptance criteria:

- no active product flow depends on runtime-command persistence or polling-era status tables
- remaining traffic persistence tables belong to an explicit collector/public-map design, not to the removed server UI

### Phase 6: Clean Tests, Tooling, And Docs

Outcome:

- tests and documentation reflect the architecture that actually exists

Tasks:

- remove or rewrite tests that exist only for deleted legacy runtime behavior
- keep product persistence tests separate from collector persistence tests
- update architecture docs to point to the cleanup result, not the pre-cutover picture
- verify all sample commands, local run instructions, and Docker entrypoints

Acceptance criteria:

- a new contributor can identify the active architecture from the repository root without reading old migration context first

## Specific Cleanup Targets

### Immediate Delete Targets

- `src/MeshBoard.Web`
- root `Dockerfile` if it is not repurposed
- `package.json` scripts:
  - `tailwind:build`
  - `tailwind:watch`
  - if they continue to point to `MeshBoard.Web`

### Immediate Rewrite Targets

- `README.md`
- `docs/PROJECT_FOUNDATIONS.md`
- `MeshBoard.slnx`

### High-Value Refactor Targets

- `src/MeshBoard.Application/DependencyInjection/ServiceCollectionExtensions.cs`
- `src/MeshBoard.Infrastructure.Persistence/DependencyInjection/ServiceCollectionExtensions.cs`
- `src/MeshBoard.Infrastructure.Meshtastic/DependencyInjection/ServiceCollectionExtensions.cs`
- `src/MeshBoard.RealtimeBridge/Program.cs`
- `src/MeshBoard.Collector/Program.cs`

## Architecture Decisions Needed Before Destructive Deletion

These decisions should be made explicitly before deeper cleanup:

- Is a public collector/public map in near-term scope?
- If yes, should it keep packet history, or only current state plus aggregate rollups?
- Should `nodes` and `neighbor_links` become collector-owned Postgres tables now?
- Is SQLite still needed anywhere after the collector path moves to PostgreSQL?

## Recommended Execution Order

1. Remove `MeshBoard.Web` from the solution, docs, and root tooling.
2. Rewrite the README and local entrypoints so the active stack is the default story.
3. Split DI registrations by runtime role.
4. Narrow `MeshBoard.Collector` around the Postgres-backed public collector decision.
5. Remove runtime-command and server-read-model leftovers that are no longer justified.
6. Finish with test and documentation cleanup.

## Done Means

This cleanup is done when:

- the repository root clearly communicates `Client + Api + RealtimeBridge` as the active architecture
- `MeshBoard.Web` is gone
- broad legacy compatibility registrations are gone
- collector/public-map code, if retained, is explicit and isolated
- product docs and sample commands no longer contradict the running system
