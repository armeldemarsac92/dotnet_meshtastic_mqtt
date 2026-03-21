# Project Foundations

This document is the current architecture reference for the repository after the browser-first cutover.

For the historical migration plan, see [ARCHITECTURE_REFACTOR_ROADMAP.md](./ARCHITECTURE_REFACTOR_ROADMAP.md).
For the current cleanup sequence, see [CLIENT_FIRST_CLEANUP_PLAN.md](./CLIENT_FIRST_CLEANUP_PLAN.md).

- Latest verification: 2026-03-21

## Active Product Architecture

The active product path is:

- `MeshBoard.Client`
  - Blazor WebAssembly UI
  - browser-side Meshtastic decryption
  - local IndexedDB-backed vault
  - in-memory projection stores for messages, channels, nodes, and map state
- `MeshBoard.Api`
  - same-origin cookie auth
  - broker/profile/favorites preference APIs
  - realtime session bootstrap
  - VerneMQ webhook authorization
- `MeshBoard.Api.SDK`
  - shared HTTP transport contracts and DI registration
- `MeshBoard.RealtimeBridge`
  - upstream MQTT consume
  - downstream raw packet republish
  - no browser-key decryption
- `VerneMQ`
  - internet-facing downstream MQTT over WSS broker
- PostgreSQL
  - product metadata and preferences

## Active Local Runtime

The active local stack is `ops/local/compose.yaml`.

It runs:

- `meshboard-client`
- `meshboard-api`
- `meshboard-realtime-bridge`
- `meshboard-vernemq`
- `meshboard-postgres`

It does not run:

- `MeshBoard.Web`
- `MeshBoard.Collector`

`meshboard-client` is the edge container. It serves the published WebAssembly app and proxies same-origin `/api/*`, `/.well-known/*`, and `/mqtt` traffic.

## Trust Boundary

The browser is the trust boundary for Meshtastic decryption keys.

Rules:

- Meshtastic decryption keys stay client-side.
- The API does not persist or require user key material.
- The bridge republishes raw packets and metadata, not decrypted plaintext.
- Client-local projections are the default source of truth for the live UI.

## Repository Structure

Active first-class projects:

```text
src/
  MeshBoard.Client/
  MeshBoard.Api/
  MeshBoard.Api.SDK/
  MeshBoard.RealtimeBridge/
  MeshBoard.Contracts/
  MeshBoard.Application/
  MeshBoard.Infrastructure.Persistence/
  MeshBoard.Infrastructure.Meshtastic/
  MeshBoard.ProductMigrationTool/
  MeshBoard.RealtimeLoadTests/
  MeshBoard.Collector/
tests/
  MeshBoard.UnitTests/
  MeshBoard.IntegrationTests/
```

Important note:

- `MeshBoard.Collector` is not part of the active product runtime.
- It is the explicit public collector candidate and owns the normalized PostgreSQL traffic schema work.

## Styling And Frontend Build

The active Tailwind input file is:

- `src/MeshBoard.Client/Styles/app.css`

The compiled stylesheet is written to:

- `src/MeshBoard.Client/wwwroot/css/app.css`

Root scripts in `package.json` now target the client build directly:

- `npm run tailwind:build`
- `npm run tailwind:watch`

## Development Defaults

Preferred local entrypoint:

- `docker compose --env-file ops/local/.env.local -f ops/local/compose.yaml up --build`

Preferred build command:

- `dotnet build MeshBoard.slnx`

Preferred test commands:

- `dotnet test tests/MeshBoard.UnitTests/MeshBoard.UnitTests.csproj`
- `dotnet test tests/MeshBoard.IntegrationTests/MeshBoard.IntegrationTests.csproj`

## Persistence Boundaries

Product persistence:

- active in `MeshBoard.Api`
- active in the bridge only for product-owned metadata registrations
- backed by PostgreSQL in the local compose stack

Collector persistence:

- owned by `MeshBoard.Collector`
- PostgreSQL-backed
- normalized around `collector_servers -> collector_channels -> collector_nodes/messages`
- includes hourly packet rollups for collector-side analytics
- prunes raw `collector_messages` history after 365 days by default
- keeps current node/link state and hourly rollups as the long-run public-map model
- first read-only public collector APIs are exposed from `MeshBoard.Api`
- those public collector endpoints are mirrored in `MeshBoard.Api.SDK` through Refit
- documented in [COLLECTOR_POSTGRES_SCHEMA.md](./COLLECTOR_POSTGRES_SCHEMA.md)

Removed legacy persistence surfaces:

- the queued runtime-command branch is gone
- the old SQLite runtime/store implementation is gone from active runtime code
- product and collector hosts now assume PostgreSQL explicitly

Remaining SQLite usage is limited to migration tooling that can read old databases and emit PostgreSQL backfill scripts.

## Architectural Guidance

Use these rules when changing the codebase:

- UI components stay thin.
- Browser realtime, crypto, worker, and IndexedDB access belong in client services, not page bodies.
- API endpoints stay thin and call application services.
- Shared transport contracts live in `MeshBoard.Api.SDK` or `MeshBoard.Contracts`, not page-local wrappers.
- Realtime infrastructure stays out of `MeshBoard.Api`.
- Product persistence and any future collector persistence should remain explicitly separated.

## Historical Note

`MeshBoard.Web` was the former Blazor Server host used during migration. It has been removed from the repository surface so the current browser-first architecture is easier to reason about.
