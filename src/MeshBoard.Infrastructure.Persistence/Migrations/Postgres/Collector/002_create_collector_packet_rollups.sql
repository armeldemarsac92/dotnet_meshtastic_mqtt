CREATE TABLE IF NOT EXISTS collector_channel_packet_hourly_rollups (
    workspace_id TEXT NOT NULL,
    channel_id BIGINT NOT NULL REFERENCES collector_channels(id) ON DELETE CASCADE,
    bucket_start_utc TIMESTAMPTZ NOT NULL,
    packet_type TEXT NOT NULL,
    packet_count INTEGER NOT NULL,
    first_seen_at_utc TIMESTAMPTZ NOT NULL,
    last_seen_at_utc TIMESTAMPTZ NOT NULL,
    PRIMARY KEY (workspace_id, channel_id, bucket_start_utc, packet_type)
);

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'collector_channel_packet_hourly_rollups'
          AND column_name = 'workspace_id') THEN
        CREATE INDEX IF NOT EXISTS ix_collector_channel_packet_hourly_rollups_workspace_bucket
            ON collector_channel_packet_hourly_rollups(workspace_id, bucket_start_utc DESC);

        CREATE INDEX IF NOT EXISTS ix_collector_channel_packet_hourly_rollups_workspace_channel_bucket
            ON collector_channel_packet_hourly_rollups(workspace_id, channel_id, bucket_start_utc DESC);
    END IF;
END $$;

CREATE TABLE IF NOT EXISTS collector_node_packet_hourly_rollups (
    workspace_id TEXT NOT NULL,
    channel_id BIGINT NOT NULL REFERENCES collector_channels(id) ON DELETE CASCADE,
    bucket_start_utc TIMESTAMPTZ NOT NULL,
    node_id TEXT NOT NULL,
    packet_type TEXT NOT NULL,
    packet_count INTEGER NOT NULL,
    first_seen_at_utc TIMESTAMPTZ NOT NULL,
    last_seen_at_utc TIMESTAMPTZ NOT NULL,
    PRIMARY KEY (workspace_id, channel_id, bucket_start_utc, node_id, packet_type)
);

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'collector_node_packet_hourly_rollups'
          AND column_name = 'workspace_id') THEN
        CREATE INDEX IF NOT EXISTS ix_collector_node_packet_hourly_rollups_workspace_bucket
            ON collector_node_packet_hourly_rollups(workspace_id, bucket_start_utc DESC);

        CREATE INDEX IF NOT EXISTS ix_collector_node_packet_hourly_rollups_workspace_node_bucket
            ON collector_node_packet_hourly_rollups(workspace_id, node_id, bucket_start_utc DESC);

        CREATE INDEX IF NOT EXISTS ix_collector_node_packet_hourly_rollups_workspace_channel_bucket
            ON collector_node_packet_hourly_rollups(workspace_id, channel_id, bucket_start_utc DESC);
    END IF;
END $$;
