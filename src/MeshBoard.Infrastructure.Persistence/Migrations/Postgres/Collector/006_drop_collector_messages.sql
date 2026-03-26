DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.tables
        WHERE table_schema = 'public'
          AND table_name = 'collector_messages') THEN
        DROP INDEX IF EXISTS ux_collector_messages_workspace_message_key;
        DROP INDEX IF EXISTS ix_collector_messages_workspace_received;
        DROP INDEX IF EXISTS ix_collector_messages_workspace_channel_received;
        DROP INDEX IF EXISTS ix_collector_messages_workspace_sender_received;
        DROP INDEX IF EXISTS ux_collector_messages_message_key;
        DROP INDEX IF EXISTS ix_collector_messages_received;
        DROP INDEX IF EXISTS ix_collector_messages_channel_received;
        DROP INDEX IF EXISTS ix_collector_messages_sender_received;

        DROP TABLE collector_messages;
    END IF;
END $$;
