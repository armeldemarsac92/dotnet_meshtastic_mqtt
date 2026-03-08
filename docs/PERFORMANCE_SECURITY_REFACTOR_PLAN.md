# Performance And Security Refactor Plan

This document defines the next major refactor direction for MeshBoard.

It is intentionally narrower than `docs/ARCHITECTURE_REFACTOR_ROADMAP.md`.
The existing roadmap establishes the multi-user direction. This document adds the missing constraint that performance improvements must not weaken private-channel security.

For the Option B implementation path that keeps the main app server-based while moving private feeds into Interactive WebAssembly, see `docs/PRIVATE_FEEDS_INTERACTIVE_WASM_PLAN.md`.

## Goal

Scale MeshBoard for many concurrent users while preserving strict confidentiality for:

- private broker credentials
- topic encryption keys
- private topic patterns
- decrypted private message content
- outbound private message payloads

Performance work is allowed to share public telemetry.
Performance work must not share private telemetry unless authorization and secret-equivalence are proven explicitly.

When there is any doubt, choose isolation over sharing.

## Current Code Findings

These findings are based on the current codebase, not on assumptions.

### 1. Runtime Scale Is Still Coupled To Workspace Count

The worker currently creates one MQTT runtime per active workspace profile.

Relevant code:

- `src/MeshBoard.Infrastructure.Meshtastic/Runtime/WorkspaceBrokerSessionManager.cs`
- `src/MeshBoard.Infrastructure.Meshtastic/Mqtt/MqttSession.cs`

This means:

- two users watching the same public feed still open separate upstream MQTT sessions
- the same upstream traffic is ingested, decoded, and projected repeatedly per workspace
- cost scales with `workspaces x feed traffic`

That is safe for isolation, but it is not efficient for public/shared traffic.

### 2. Secret Material Is Stored In Plaintext Configuration Tables

The current schema stores the following directly in SQLite:

- broker passwords
- default broker encryption keys
- topic preset encryption keys

Relevant code:

- `src/MeshBoard.Infrastructure.Persistence/SQL/SchemaQueries.cs`
- `src/MeshBoard.Infrastructure.Persistence/Repositories/TopicPresetRepository.cs`
- `src/MeshBoard.Application/Services/WorkspaceProvisioningService.cs`

Current columns of concern:

- `broker_server_profiles.password`
- `broker_server_profiles.default_encryption_key_base64`
- `topic_presets.encryption_key_base64`

This is the biggest at-rest security gap in the current design.

### 3. Decrypted Private Content Is Persisted Like Ordinary Message History

The ingest path writes `PayloadPreview` and `IsPrivate` into `message_history`.
That includes decrypted private text when a workspace has the right key.

Relevant code:

- `src/MeshBoard.Infrastructure.Meshtastic/Decoding/MeshtasticEnvelopeReader.cs`
- `src/MeshBoard.Application/Services/MeshtasticIngestionService.cs`
- `src/MeshBoard.Infrastructure.Persistence/SQL/MessageQueries.cs`

This is acceptable only while message history is fully isolated per workspace and the database is trusted.
It is not acceptable if future performance work introduces shared projections without a stronger confidentiality model.

### 4. Outbound Runtime Commands Persist Payloads In Plaintext

The durable runtime command queue stores message payloads in the `payload` column.
That includes outbound private send requests.

Relevant code:

- `src/MeshBoard.Application/Services/MessageComposerService.cs`
- `src/MeshBoard.Infrastructure.Persistence/Runtime/SqliteBrokerRuntimeCommandRepository.cs`
- `src/MeshBoard.Infrastructure.Persistence/SQL/BrokerRuntimeCommandQueries.cs`

This is another at-rest secret-handling gap.

### 5. Auth Hardening Is Functional But Not Yet High-Security

The web app now has username/password cookies and workspace claims, which is correct structurally.
However:

- auth endpoints disable antiforgery
- cookies are not yet tightened for production-grade hardening
- there is no login throttling or lockout
- there is no session revocation model

Relevant code:

- `src/MeshBoard.Web/Authentication/AuthEndpointMappings.cs`
- `src/MeshBoard.Web/Program.cs`
- `src/MeshBoard.Application/Services/UserAccountService.cs`
- `src/MeshBoard.Application/Authentication/PasswordHashingService.cs`

### 6. Topic Discovery Must Never Become Globally Shared By Accident

Topic discovery is now workspace-scoped, which is correct.
That must remain true for private topics, because the topic pattern itself may be sensitive.

Relevant code:

- `src/MeshBoard.Application/Services/TopicDiscoveryService.cs`
- `src/MeshBoard.Infrastructure.Persistence/Repositories/DiscoveredTopicRepository.cs`

## Non-Negotiable Security Rules

Future agents should treat these as hard constraints.

- Never store raw private-channel keys in ordinary configuration tables.
- Never share decrypted private message content across workspaces.
- Never share private topic discovery across workspaces.
- Never write outbound private send payloads to a plaintext durable queue.
- Never log broker passwords, topic keys, or decrypted private payloads.
- Never classify a feed as shareable unless that decision is explicit and test-covered.
- If a feed uses custom broker credentials or a non-default encryption key, default to isolated processing.

## Feed Confidentiality Model

All performance work should begin by classifying each feed/profile into one of these classes.

### `PublicShared`

A feed is `PublicShared` only if all of the following are true:

- broker credentials are public/shared defaults or the broker is anonymous
- no workspace-specific broker secret is used
- no custom topic encryption key is used
- only the Meshtastic public/default key is used
- topic patterns belong to an approved public/shared allowlist

This class is allowed to use shared runtime sessions, shared raw packet ingest, and shared canonical projections.

### `SensitiveIsolated`

A feed is `SensitiveIsolated` if any of the following are true:

- broker username/password is workspace-specific
- a topic preset defines a custom encryption key
- the active profile defines a custom default encryption key
- the topic pattern is not explicitly classified as public/shared

This class must remain isolated by workspace unless a future design introduces explicit shared-secret domains with strong authorization.

That shared-secret-domain optimization is not part of this plan.

## Target Architecture

The target architecture is intentionally hybrid.

Public data should be shared for scale.
Private data should remain isolated for security.

### Public Path

`MQTT shared runtime -> canonical raw packets -> public projections -> per-user UI overlays`

Use this path only for `PublicShared` feeds.

Properties:

- one upstream MQTT session per canonical public feed identity
- one canonical packet ingest for public traffic
- shared node and channel projections
- user-specific preferences remain separate

### Secure Path

`MQTT isolated runtime -> secure packet store -> secure projections -> authorized workspace reads`

Use this path for `SensitiveIsolated` feeds.

Properties:

- runtime isolation remains workspace-scoped
- secret resolution is workspace-scoped
- topic discovery remains workspace-scoped
- decrypted content is never shared outside the workspace security boundary

## Data Model Refactor

### Introduce Feed Classification

Add explicit classification instead of relying on implicit behavior.

Recommended concepts:

- `FeedScopeId`
- `FeedConfidentialityClass`
- `IsSharedEligible`
- `SecretFingerprint`

Suggested entities:

- `feed_profiles`
- `feed_scopes`
- `workspace_feed_access`

Important rule:

- queryable profile metadata may contain a key fingerprint
- queryable profile metadata must not contain the raw key

### Separate Shared And Secure Storage

Do not keep using one message table for everything.

Recommended split:

- `public_packet_ingest`
- `public_message_projection`
- `public_node_projection`
- `public_channel_projection`
- `workspace_secure_packet`
- `workspace_secure_message_projection`
- `workspace_secure_node_projection`

`public_*` tables may be shared.
`workspace_secure_*` tables must remain workspace-scoped.

### Remove Plaintext Secret Columns

Move secrets out of ordinary tables.

Recommended direction:

- replace plaintext secret columns with secret references
- encrypt secrets at rest using envelope encryption
- keep the master key outside the database

Suggested concepts:

- `workspace_secret`
- `secret_reference`
- `secret_version`
- `secret_fingerprint`

Store:

- encrypted broker password
- encrypted default topic key
- encrypted preset topic keys

Do not store:

- decrypted key bytes in regular SQL result DTOs

### Redesign Private Message Retention

Current behavior stores decrypted private preview text in `message_history`.
That is convenient, but it is the wrong default for a high-security system.

Recommended modes:

#### Default: Live-Only Private Content

- persist enough metadata for activity, counts, timestamps, and node updates
- do not persist decrypted private payload text at rest
- render private payload content only in live authorized flows

#### Optional: Encrypted Private Retention

If the product requires private history retention:

- persist decrypted private content only after encrypting it with a workspace-scoped data key
- keep encrypted payloads in secure projection tables only
- never expose those encrypted rows to shared/public query paths

Start with `Live-Only Private Content`.
It is the safest balance.

## Runtime Refactor

### Public Feed Pooling

For `PublicShared` feeds:

- pool MQTT sessions by canonical public feed identity
- ingest packets once
- publish one canonical public projection stream

Canonical public feed identity should include:

- broker host
- broker port
- TLS mode
- public credential identity
- approved public topic root set

It must not include workspace id.

### Secure Feed Isolation

For `SensitiveIsolated` feeds:

- keep runtime ownership isolated per workspace
- resolve secrets only inside the secure runtime path
- avoid promoting secure feed data into shared caches or shared projections

### Decode Strategy

Split decode into two stages:

#### Stage 1: Transport Parse

Safe for both public and secure paths:

- broker server
- topic
- packet id
- sender/recipient ids when available
- raw encrypted payload bytes
- receive time

#### Stage 2: Decrypt And Interpret

Security-sensitive:

- requires access to workspace secrets
- may produce decrypted content
- must run only inside the appropriate confidentiality boundary

This split allows performance improvements for public traffic without forcing private-key material into shared code paths.

## Command Queue Refactor

The runtime command queue currently stores payloads in plaintext.
That is not acceptable for private sends.

Required changes:

- add a secure command envelope for private outbound messages
- encrypt queued outbound payloads at rest
- keep only routing metadata in plaintext where necessary
- ensure worker decryption occurs only at command execution time

Recommended split:

- `broker_runtime_commands` for non-sensitive routing metadata
- `broker_runtime_command_payloads_secure` for encrypted payload bodies

## Auth And Web Security Hardening

Before this app is exposed broadly, complete these changes:

- enable CSRF protection for auth form posts
- tighten cookie policy:
  - `HttpOnly`
  - `SecurePolicy = Always` outside development
  - explicit `SameSite`
- add login throttling per username and IP
- add password rehash-on-login support so hash cost can increase safely
- add session revocation or security-stamp validation
- move operational diagnostics behind admin-only authorization

## Performance Roadmap

This section is the recommended order of implementation.

### Phase A: Secure Foundations

Objective:

- stop unsafe secret persistence before attempting major sharing optimizations

Tasks:

- introduce secret store abstractions
- remove plaintext broker password and key columns from live usage
- encrypt outbound private command payloads
- define `FeedConfidentialityClass`

Acceptance:

- raw keys are no longer returned by ordinary repositories
- private outbound payloads are not persisted in plaintext
- feed classification exists and is test-covered

### Phase B: Shared Public Ingest

Objective:

- eliminate duplicated ingest for public traffic

Tasks:

- add public feed identity model
- pool runtimes for `PublicShared`
- ingest packets once into canonical public storage
- move public node/message/channel projections to shared tables

Acceptance:

- two users on the same public feed do not create two upstream MQTT sessions
- public packets are ingested once
- user preferences remain isolated

### Phase C: Secure Private Retention

Objective:

- make private traffic safe at rest

Tasks:

- remove plaintext private payload previews from the default write path
- add live-only private rendering first
- optionally add encrypted private retention after the live-only path is stable

Acceptance:

- decrypted private content is no longer stored in plaintext
- private topic discovery remains workspace-scoped

### Phase D: Query And Push Optimization

Objective:

- scale reads and realtime delivery after the confidentiality boundaries are correct

Tasks:

- move push delivery to group-targeted SignalR/backplane delivery
- stop polling the projection log from every web instance without coordination
- cache public shared read models aggressively
- keep secure read-model caching inside the workspace boundary only

Acceptance:

- push fan-out is authorization-safe
- cache keys encode the right confidentiality boundary

### Phase E: Database Upgrade

Objective:

- move to PostgreSQL after the data model is stabilized

Why last:

- provider migration before confidentiality-model cleanup would lock in the wrong schema

Tasks:

- port the new shared/secure split schema to PostgreSQL
- keep SQLite only for local/dev/test mode if still needed

Acceptance:

- PostgreSQL schema reflects public/shared versus secure/isolated boundaries directly

## Forbidden Optimizations

Do not implement any of the following:

- sharing decrypted private packets across workspaces
- building a global discovered-topic index that includes private channels
- caching private payload previews in host-global memory
- logging or surfacing encryption keys in diagnostics
- storing outbound private message payloads in plaintext queues
- treating all feeds on the same broker as shareable

## Required Tests

The following test coverage must exist before this refactor is considered complete.

- cross-workspace isolation for private topic discovery
- cross-workspace isolation for decrypted private message history
- proof that public shared feeds dedupe ingest across users
- proof that sensitive feeds do not share runtime sessions
- proof that secret material is encrypted at rest
- proof that private outbound command payloads are encrypted at rest
- authorization tests for diagnostics and realtime delivery

## Recommended First Implementation Slice

Do not start by pooling every feed.
Start with the security boundary.

Recommended first slice:

1. introduce `FeedConfidentialityClass`
2. add a secure secret store abstraction for broker passwords and topic keys
3. remove plaintext secret reads from ordinary repositories
4. stop persisting private outbound payloads in plaintext
5. stop persisting decrypted private message preview text by default

Only after that should the codebase start sharing public feed runtimes and public projections.
