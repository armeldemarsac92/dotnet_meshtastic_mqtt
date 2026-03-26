CREATE TABLE IF NOT EXISTS collector_neighbor_link_hourly_rollups (
    workspace_id TEXT NOT NULL,
    channel_id BIGINT NOT NULL REFERENCES collector_channels(id) ON DELETE CASCADE,
    bucket_start_utc TIMESTAMPTZ NOT NULL,
    source_node_id TEXT NOT NULL,
    target_node_id TEXT NOT NULL,
    observation_count INTEGER NOT NULL,
    snr_sample_count INTEGER NOT NULL,
    snr_sum_db DOUBLE PRECISION NOT NULL,
    max_snr_db REAL NULL,
    last_snr_db REAL NULL,
    first_seen_at_utc TIMESTAMPTZ NOT NULL,
    last_seen_at_utc TIMESTAMPTZ NOT NULL,
    PRIMARY KEY (workspace_id, channel_id, bucket_start_utc, source_node_id, target_node_id)
);

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'collector_neighbor_link_hourly_rollups'
          AND column_name = 'workspace_id') THEN
        CREATE INDEX IF NOT EXISTS ix_collector_neighbor_link_hourly_rollups_workspace_bucket
            ON collector_neighbor_link_hourly_rollups(workspace_id, bucket_start_utc DESC);

        CREATE INDEX IF NOT EXISTS ix_collector_neighbor_link_hourly_rollups_workspace_link_bucket
            ON collector_neighbor_link_hourly_rollups(workspace_id, source_node_id, target_node_id, bucket_start_utc DESC);

        CREATE INDEX IF NOT EXISTS ix_collector_neighbor_link_hourly_rollups_workspace_channel_bucket
            ON collector_neighbor_link_hourly_rollups(workspace_id, channel_id, bucket_start_utc DESC);
    END IF;
END $$;
