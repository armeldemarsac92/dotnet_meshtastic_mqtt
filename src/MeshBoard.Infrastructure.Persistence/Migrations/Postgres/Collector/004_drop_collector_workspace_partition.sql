DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'collector_servers'
          AND column_name = 'workspace_id') THEN
        DELETE FROM collector_servers
        WHERE workspace_id <> 'default';

        DROP INDEX IF EXISTS ux_collector_servers_workspace_address;
        DROP INDEX IF EXISTS ux_collector_channels_workspace_server_scope;
        DROP INDEX IF EXISTS ix_collector_channels_workspace_server_seen;
        DROP INDEX IF EXISTS ux_collector_nodes_workspace_channel_node;
        DROP INDEX IF EXISTS ix_collector_nodes_workspace_last_heard;
        DROP INDEX IF EXISTS ix_collector_nodes_workspace_position;
        DROP INDEX IF EXISTS ix_collector_neighbor_links_workspace_seen;
        DROP INDEX IF EXISTS ix_collector_channel_packet_hourly_rollups_workspace_bucket;
        DROP INDEX IF EXISTS ix_collector_channel_packet_hourly_rollups_workspace_channel_bucket;
        DROP INDEX IF EXISTS ix_collector_node_packet_hourly_rollups_workspace_bucket;
        DROP INDEX IF EXISTS ix_collector_node_packet_hourly_rollups_workspace_node_bucket;
        DROP INDEX IF EXISTS ix_collector_node_packet_hourly_rollups_workspace_channel_bucket;
        DROP INDEX IF EXISTS ix_collector_neighbor_link_hourly_rollups_workspace_bucket;
        DROP INDEX IF EXISTS ix_collector_neighbor_link_hourly_rollups_workspace_link_bucket;
        DROP INDEX IF EXISTS ix_collector_neighbor_link_hourly_rollups_workspace_channel_bucket;

        ALTER TABLE collector_neighbor_links
            DROP CONSTRAINT IF EXISTS collector_neighbor_links_pkey;

        ALTER TABLE collector_channel_packet_hourly_rollups
            DROP CONSTRAINT IF EXISTS collector_channel_packet_hourly_rollups_pkey;

        ALTER TABLE collector_node_packet_hourly_rollups
            DROP CONSTRAINT IF EXISTS collector_node_packet_hourly_rollups_pkey;

        ALTER TABLE collector_neighbor_link_hourly_rollups
            DROP CONSTRAINT IF EXISTS collector_neighbor_link_hourly_rollups_pkey;

        ALTER TABLE collector_servers
            DROP COLUMN IF EXISTS workspace_id;

        ALTER TABLE collector_channels
            DROP COLUMN IF EXISTS workspace_id;

        ALTER TABLE collector_nodes
            DROP COLUMN IF EXISTS workspace_id;

        ALTER TABLE collector_neighbor_links
            DROP COLUMN IF EXISTS workspace_id;

        ALTER TABLE collector_channel_packet_hourly_rollups
            DROP COLUMN IF EXISTS workspace_id;

        ALTER TABLE collector_node_packet_hourly_rollups
            DROP COLUMN IF EXISTS workspace_id;

        ALTER TABLE collector_neighbor_link_hourly_rollups
            DROP COLUMN IF EXISTS workspace_id;

        CREATE UNIQUE INDEX IF NOT EXISTS ux_collector_servers_server_address
            ON collector_servers(server_address);

        CREATE UNIQUE INDEX IF NOT EXISTS ux_collector_channels_server_scope
            ON collector_channels(server_id, region, mesh_version, channel_name);

        CREATE INDEX IF NOT EXISTS ix_collector_channels_server_seen
            ON collector_channels(server_id, last_observed_at_utc DESC);

        CREATE UNIQUE INDEX IF NOT EXISTS ux_collector_nodes_channel_node
            ON collector_nodes(channel_id, node_id);

        CREATE INDEX IF NOT EXISTS ix_collector_nodes_last_heard
            ON collector_nodes(last_heard_at_utc DESC NULLS LAST);

        CREATE INDEX IF NOT EXISTS ix_collector_nodes_position
            ON collector_nodes(channel_id)
            WHERE last_known_latitude IS NOT NULL AND last_known_longitude IS NOT NULL;

        ALTER TABLE collector_neighbor_links
            ADD CONSTRAINT collector_neighbor_links_pkey
                PRIMARY KEY (channel_id, source_node_id, target_node_id);

        CREATE INDEX IF NOT EXISTS ix_collector_neighbor_links_seen
            ON collector_neighbor_links(last_seen_at_utc DESC);

        ALTER TABLE collector_channel_packet_hourly_rollups
            ADD CONSTRAINT collector_channel_packet_hourly_rollups_pkey
                PRIMARY KEY (channel_id, bucket_start_utc, packet_type);

        CREATE INDEX IF NOT EXISTS ix_collector_channel_packet_hourly_rollups_bucket
            ON collector_channel_packet_hourly_rollups(bucket_start_utc DESC);

        CREATE INDEX IF NOT EXISTS ix_collector_channel_packet_hourly_rollups_channel_bucket
            ON collector_channel_packet_hourly_rollups(channel_id, bucket_start_utc DESC);

        ALTER TABLE collector_node_packet_hourly_rollups
            ADD CONSTRAINT collector_node_packet_hourly_rollups_pkey
                PRIMARY KEY (channel_id, bucket_start_utc, node_id, packet_type);

        CREATE INDEX IF NOT EXISTS ix_collector_node_packet_hourly_rollups_bucket
            ON collector_node_packet_hourly_rollups(bucket_start_utc DESC);

        CREATE INDEX IF NOT EXISTS ix_collector_node_packet_hourly_rollups_node_bucket
            ON collector_node_packet_hourly_rollups(node_id, bucket_start_utc DESC);

        CREATE INDEX IF NOT EXISTS ix_collector_node_packet_hourly_rollups_channel_bucket
            ON collector_node_packet_hourly_rollups(channel_id, bucket_start_utc DESC);

        ALTER TABLE collector_neighbor_link_hourly_rollups
            ADD CONSTRAINT collector_neighbor_link_hourly_rollups_pkey
                PRIMARY KEY (channel_id, bucket_start_utc, source_node_id, target_node_id);

        CREATE INDEX IF NOT EXISTS ix_collector_neighbor_link_hourly_rollups_bucket
            ON collector_neighbor_link_hourly_rollups(bucket_start_utc DESC);

        CREATE INDEX IF NOT EXISTS ix_collector_neighbor_link_hourly_rollups_link_bucket
            ON collector_neighbor_link_hourly_rollups(source_node_id, target_node_id, bucket_start_utc DESC);

        CREATE INDEX IF NOT EXISTS ix_collector_neighbor_link_hourly_rollups_channel_bucket
            ON collector_neighbor_link_hourly_rollups(channel_id, bucket_start_utc DESC);
    END IF;
END $$;
