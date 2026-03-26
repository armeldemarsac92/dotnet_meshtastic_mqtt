DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_indexes
        WHERE schemaname = 'public'
          AND tablename = 'collector_neighbor_links'
          AND indexname = 'ix_collector_neighbor_links_channel_seen') THEN
        CREATE INDEX ix_collector_neighbor_links_channel_seen
            ON collector_neighbor_links(channel_id, last_seen_at_utc DESC);
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_indexes
        WHERE schemaname = 'public'
          AND tablename = 'collector_nodes'
          AND indexname = 'ix_collector_nodes_server_last_heard_coalesce') THEN
        CREATE INDEX ix_collector_nodes_server_last_heard_coalesce
            ON collector_nodes(server_id, COALESCE(last_heard_at_utc, last_text_message_at_utc) DESC NULLS LAST);
    END IF;
END $$;
