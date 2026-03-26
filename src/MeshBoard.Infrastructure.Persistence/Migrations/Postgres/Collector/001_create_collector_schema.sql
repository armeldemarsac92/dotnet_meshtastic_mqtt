CREATE TABLE IF NOT EXISTS collector_servers (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    workspace_id TEXT NOT NULL,
    server_address TEXT NOT NULL,
    first_observed_at_utc TIMESTAMPTZ NOT NULL,
    last_observed_at_utc TIMESTAMPTZ NOT NULL
);

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'collector_servers'
          AND column_name = 'workspace_id') THEN
        CREATE UNIQUE INDEX IF NOT EXISTS ux_collector_servers_workspace_address
            ON collector_servers(workspace_id, server_address);
    END IF;
END $$;

CREATE TABLE IF NOT EXISTS collector_channels (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    workspace_id TEXT NOT NULL,
    server_id BIGINT NOT NULL REFERENCES collector_servers(id) ON DELETE CASCADE,
    region TEXT NOT NULL,
    mesh_version TEXT NOT NULL,
    channel_name TEXT NOT NULL,
    topic_pattern TEXT NOT NULL,
    first_observed_at_utc TIMESTAMPTZ NOT NULL,
    last_observed_at_utc TIMESTAMPTZ NOT NULL
);

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'collector_channels'
          AND column_name = 'workspace_id') THEN
        CREATE UNIQUE INDEX IF NOT EXISTS ux_collector_channels_workspace_server_scope
            ON collector_channels(workspace_id, server_id, region, mesh_version, channel_name);

        CREATE INDEX IF NOT EXISTS ix_collector_channels_workspace_server_seen
            ON collector_channels(workspace_id, server_id, last_observed_at_utc DESC);
    END IF;
END $$;

CREATE TABLE IF NOT EXISTS collector_nodes (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    workspace_id TEXT NOT NULL,
    channel_id BIGINT NOT NULL REFERENCES collector_channels(id) ON DELETE CASCADE,
    node_id TEXT NOT NULL,
    short_name TEXT NULL,
    long_name TEXT NULL,
    last_heard_at_utc TIMESTAMPTZ NULL,
    last_text_message_at_utc TIMESTAMPTZ NULL,
    last_known_latitude DOUBLE PRECISION NULL,
    last_known_longitude DOUBLE PRECISION NULL,
    battery_level_percent INTEGER NULL,
    voltage DOUBLE PRECISION NULL,
    channel_utilization DOUBLE PRECISION NULL,
    air_util_tx DOUBLE PRECISION NULL,
    uptime_seconds BIGINT NULL,
    temperature_celsius DOUBLE PRECISION NULL,
    relative_humidity DOUBLE PRECISION NULL,
    barometric_pressure DOUBLE PRECISION NULL
);

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'collector_nodes'
          AND column_name = 'workspace_id') THEN
        CREATE UNIQUE INDEX IF NOT EXISTS ux_collector_nodes_workspace_channel_node
            ON collector_nodes(workspace_id, channel_id, node_id);

        CREATE INDEX IF NOT EXISTS ix_collector_nodes_workspace_last_heard
            ON collector_nodes(workspace_id, last_heard_at_utc DESC NULLS LAST);

        CREATE INDEX IF NOT EXISTS ix_collector_nodes_workspace_position
            ON collector_nodes(workspace_id, channel_id)
            WHERE last_known_latitude IS NOT NULL AND last_known_longitude IS NOT NULL;
    END IF;
END $$;

CREATE TABLE IF NOT EXISTS collector_neighbor_links (
    workspace_id TEXT NOT NULL,
    channel_id BIGINT NOT NULL REFERENCES collector_channels(id) ON DELETE CASCADE,
    source_node_id TEXT NOT NULL,
    target_node_id TEXT NOT NULL,
    snr_db REAL NULL,
    last_seen_at_utc TIMESTAMPTZ NOT NULL,
    PRIMARY KEY (workspace_id, channel_id, source_node_id, target_node_id)
);

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'collector_neighbor_links'
          AND column_name = 'workspace_id') THEN
        CREATE INDEX IF NOT EXISTS ix_collector_neighbor_links_workspace_seen
            ON collector_neighbor_links(workspace_id, last_seen_at_utc DESC);
    END IF;
END $$;
