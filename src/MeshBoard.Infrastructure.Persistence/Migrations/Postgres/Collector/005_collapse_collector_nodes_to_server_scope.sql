DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'collector_nodes'
          AND column_name = 'channel_id') THEN
        ALTER TABLE collector_nodes
            ADD COLUMN IF NOT EXISTS server_id BIGINT;

        ALTER TABLE collector_nodes
            ADD COLUMN IF NOT EXISTS last_heard_channel_id BIGINT NULL;

        UPDATE collector_nodes n
        SET server_id = c.server_id,
            last_heard_channel_id = COALESCE(n.last_heard_channel_id, n.channel_id)
        FROM collector_channels c
        WHERE c.id = n.channel_id
          AND (n.server_id IS NULL OR n.last_heard_channel_id IS NULL);

        DELETE FROM collector_nodes duplicate_nodes
        USING (
            SELECT id
            FROM (
                SELECT
                    n.id,
                    ROW_NUMBER() OVER (
                        PARTITION BY n.server_id, n.node_id
                        ORDER BY COALESCE(n.last_heard_at_utc, n.last_text_message_at_utc) DESC NULLS LAST,
                                 n.id DESC) AS row_number
                FROM collector_nodes n
            ) ranked_nodes
            WHERE ranked_nodes.row_number > 1
        ) duplicates
        WHERE duplicate_nodes.id = duplicates.id;

        ALTER TABLE collector_nodes
            ALTER COLUMN server_id SET NOT NULL;

        ALTER TABLE collector_nodes
            ADD CONSTRAINT fk_collector_nodes_server
                FOREIGN KEY (server_id) REFERENCES collector_servers(id) ON DELETE CASCADE;

        ALTER TABLE collector_nodes
            ADD CONSTRAINT fk_collector_nodes_last_heard_channel
                FOREIGN KEY (last_heard_channel_id) REFERENCES collector_channels(id) ON DELETE SET NULL;

        DROP INDEX IF EXISTS ux_collector_nodes_channel_node;
        DROP INDEX IF EXISTS ix_collector_nodes_last_heard;
        DROP INDEX IF EXISTS ix_collector_nodes_position;

        ALTER TABLE collector_nodes
            DROP COLUMN channel_id CASCADE;

        CREATE UNIQUE INDEX IF NOT EXISTS ux_collector_nodes_server_node
            ON collector_nodes(server_id, node_id);

        CREATE INDEX IF NOT EXISTS ix_collector_nodes_server_last_heard
            ON collector_nodes(server_id, last_heard_at_utc DESC NULLS LAST);

        CREATE INDEX IF NOT EXISTS ix_collector_nodes_last_heard
            ON collector_nodes(last_heard_at_utc DESC NULLS LAST);

        CREATE INDEX IF NOT EXISTS ix_collector_nodes_last_heard_channel
            ON collector_nodes(last_heard_channel_id, last_heard_at_utc DESC NULLS LAST);

        CREATE INDEX IF NOT EXISTS ix_collector_nodes_server_position
            ON collector_nodes(server_id)
            WHERE last_known_latitude IS NOT NULL AND last_known_longitude IS NOT NULL;
    END IF;
END $$;
