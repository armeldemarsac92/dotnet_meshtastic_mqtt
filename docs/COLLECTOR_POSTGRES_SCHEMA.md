# Collector PostgreSQL Schema

- Status: Drafted and partially implemented
- Date: 2026-03-21
- Scope: `MeshBoard.Collector` traffic persistence

## Purpose

The collector schema is separate from product preferences/auth persistence.

Its goals are:

- normalize broker server and channel metadata instead of repeating them on every row
- keep channels and links normalized without repeating server metadata
- keep nodes server-scoped with their last heard channel as metadata
- keep radio links attached to the channel that reported them
- write directly into bounded hourly rollups for packet-type analytics

Current policy:

- `collector_nodes` and `collector_neighbor_links` are current-state tables and are not expired automatically
- hourly rollup tables are the long-run aggregate layer and are not expired automatically

## Core Tables

### `collector_servers`

One row per observed upstream broker server within a workspace.

Key columns:

- `id`
- `workspace_id`
- `server_address`
- `first_observed_at_utc`
- `last_observed_at_utc`

Uniqueness:

- `(workspace_id, server_address)`

### `collector_channels`

One row per observed channel on a server.

Key columns:

- `id`
- `workspace_id`
- `server_id`
- `region`
- `mesh_version`
- `channel_name`
- `topic_pattern`
- `first_observed_at_utc`
- `last_observed_at_utc`

Relationships:

- `server_id -> collector_servers.id`

Uniqueness:

- `(workspace_id, server_id, region, mesh_version, channel_name)`

### `collector_nodes`

Latest server-scoped node snapshot.

Key columns:

- `id`
- `server_id`
- `last_heard_channel_id`
- `node_id`
- `short_name`
- `long_name`
- `last_heard_at_utc`
- `last_text_message_at_utc`
- `last_known_latitude`
- `last_known_longitude`
- telemetry columns

Relationships:

- `server_id -> collector_servers.id`
- `last_heard_channel_id -> collector_channels.id`

Uniqueness:

- `(server_id, node_id)`

### `collector_neighbor_links`

Current channel-scoped radio-link graph.

Key columns:

- `workspace_id`
- `channel_id`
- `source_node_id`
- `target_node_id`
- `snr_db`
- `last_seen_at_utc`

Relationships:

- `channel_id -> collector_channels.id`

Uniqueness:

- `(workspace_id, channel_id, source_node_id, target_node_id)`

Notes:

- this is currently the simplified canonical edge table
- if directional asymmetry becomes important, extend this table with directional columns instead of adding a raw observation log immediately

## Retention

Collector retention is intentionally split by data shape:

- the collector does not persist raw per-message history
- the current-state node and link tables are not pruned automatically
- hourly rollups are retained as the durable analytical history

### `collector_channel_packet_hourly_rollups`

Hourly packet-type counts per channel.

Key columns:

- `workspace_id`
- `channel_id`
- `bucket_start_utc`
- `packet_type`
- `packet_count`
- `first_seen_at_utc`
- `last_seen_at_utc`

Relationships:

- `channel_id -> collector_channels.id`

Uniqueness:

- `(workspace_id, channel_id, bucket_start_utc, packet_type)`

### `collector_node_packet_hourly_rollups`

Hourly packet-type counts per node within a channel.

Key columns:

- `workspace_id`
- `channel_id`
- `bucket_start_utc`
- `node_id`
- `packet_type`
- `packet_count`
- `first_seen_at_utc`
- `last_seen_at_utc`

Relationships:

- `channel_id -> collector_channels.id`

Uniqueness:

- `(workspace_id, channel_id, bucket_start_utc, node_id, packet_type)`

### `collector_neighbor_link_hourly_rollups`

Hourly link-observation counts and SNR aggregates per canonical link within a channel.

Key columns:

- `workspace_id`
- `channel_id`
- `bucket_start_utc`
- `source_node_id`
- `target_node_id`
- `observation_count`
- `snr_sample_count`
- `snr_sum_db`
- `max_snr_db`
- `last_snr_db`
- `first_seen_at_utc`
- `last_seen_at_utc`

Relationships:

- `channel_id -> collector_channels.id`

Uniqueness:

- `(workspace_id, channel_id, bucket_start_utc, source_node_id, target_node_id)`

## Why This Shape

Compared to the old denormalized collector tables:

- server metadata moves out of `nodes` and `message_history`
- channel metadata moves out of `nodes`, `messages`, and topic-discovery rows
- the collector can answer channel-scoped questions without re-parsing topics every time
- product persistence stays distinct from collector traffic persistence

## Read Side

The first read-only public collector endpoints now exist in the API:

- `GET /api/public/collector/servers`
- `GET /api/public/collector/channels`
- `GET /api/public/collector/snapshot`
- `GET /api/public/collector/stats/channel-packets`
- `GET /api/public/collector/stats/node-packets`
- `GET /api/public/collector/stats/neighbor-links`
- `GET /api/public/collector/overview`
- `GET /api/public/collector/topology`

Those endpoints are intentionally limited to current-state map reads and bounded hourly analytics over the normalized collector tables.

The public collector surface is also mirrored in `MeshBoard.Api.SDK` through a single Refit client so browser and tooling consumers do not need to hand-roll HTTP wrappers for the collector read side.

The topology endpoint is intentionally broader than the map snapshot:

- it resolves active nodes even when they do not have coordinates
- it computes connected components, isolated nodes, articulation-point bridge nodes, and strongest active links
- it uses the current `collector_neighbor_links` graph plus hourly link rollups for strength/stability ranking

The overview endpoint is intentionally lighter than topology:

- it groups the read model as `server -> channels`
- it summarizes active nodes, active links, packet mix, and neighbor-observation counts for a bounded channel set
- it is meant to be the default public-map landing query when a client needs high-signal health/shape data without fetching raw node/link payloads

## Not Included Yet

Deliberately still out of scope for the first Postgres collector pass:

- raw neighbor-link observation history
- exact historical location trails
- analytics-oriented aggregate APIs

Those should be added only when the longer-term collector privacy and storage policy is revisited.
