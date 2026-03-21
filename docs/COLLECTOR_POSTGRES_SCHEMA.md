# Collector PostgreSQL Schema

- Status: Drafted and partially implemented
- Date: 2026-03-21
- Scope: `MeshBoard.Collector` traffic persistence

## Purpose

The collector schema is separate from product preferences/auth persistence.

Its goals are:

- normalize broker server and channel metadata instead of repeating them on every row
- keep messages and nodes attached to a channel
- keep radio links attached to the channel that reported them
- add bounded hourly rollups for packet-type analytics without forcing raw-history retention decisions

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

Latest channel-scoped node snapshot.

Key columns:

- `id`
- `workspace_id`
- `channel_id`
- `node_id`
- `short_name`
- `long_name`
- `last_heard_at_utc`
- `last_text_message_at_utc`
- `last_known_latitude`
- `last_known_longitude`
- telemetry columns

Relationships:

- `channel_id -> collector_channels.id`

Uniqueness:

- `(workspace_id, channel_id, node_id)`

Notes:

- the same node id can exist on multiple channels
- read models that want one row per node should collapse to the latest channel-scoped row

### `collector_messages`

Observed packet/message history with channel ownership.

Key columns:

- `id`
- `workspace_id`
- `channel_id`
- `message_key`
- `topic`
- `packet_type`
- `from_node_id`
- `to_node_id`
- `payload_preview`
- `is_private`
- `received_at_utc`

Relationships:

- `channel_id -> collector_channels.id`

Uniqueness:

- `(workspace_id, message_key)`

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
- `GET /api/public/collector/topology`

Those endpoints are intentionally limited to current-state map reads and bounded hourly analytics over the normalized collector tables.

The topology endpoint is intentionally broader than the map snapshot:

- it resolves active nodes even when they do not have coordinates
- it computes connected components, isolated nodes, articulation-point bridge nodes, and strongest active links
- it uses the current `collector_neighbor_links` graph plus hourly link rollups for strength/stability ranking

## Not Included Yet

Deliberately still out of scope for the first Postgres collector pass:

- raw neighbor-link observation history
- exact historical location trails
- analytics-oriented aggregate APIs

Those should be added only after retention/privacy policy is explicit.
