# ADR 0001: Operating Model

- Status: Accepted
- Date: 2026-03-08

## Context

MeshBoard currently behaves like a local operator console:

- singleton MQTT runtime
- global server switching
- globally scoped favorites and topic presets
- SQLite as the main live and historical store
- page-driven polling for live updates

This architecture is acceptable only for a small local deployment.

The product direction has now been clarified: MeshBoard must be a web application used by many users.

## Decision

MeshBoard is an accepted `multi-user-web` system.

The architecture must therefore evolve toward these rules:

- user-visible settings and active selections must be scoped by user or workspace
- MQTT runtime ownership must not live inside a process-global UI-oriented singleton model
- ingestion and web delivery should be separated into clear runtime responsibilities
- PostgreSQL or another server-grade relational database becomes the production primary store
- push-based update delivery is preferred over aggressive database polling
- any cache or pubsub layer must respect workspace isolation and authorization

## Consequences

### Immediate consequences

- the current singleton MQTT session model is transitional only
- current process-global active server behavior is not acceptable long term
- hidden write-on-read behavior must be removed
- screen-specific read paths need proper query services and projections

### Near-term consequences

- schema changes will be required to introduce user/workspace boundaries
- repository and service signatures will need scope-aware parameters
- an ingestion worker or clearly separated runtime boundary will be introduced
- PostgreSQL migration should happen before deeper scale work

### Long-term consequences

- the system should support horizontal scale-out
- realtime delivery should use push invalidation rather than frequent polling
- operational observability must cover ingest lag, queue saturation, query latency, and authorization-safe event delivery
