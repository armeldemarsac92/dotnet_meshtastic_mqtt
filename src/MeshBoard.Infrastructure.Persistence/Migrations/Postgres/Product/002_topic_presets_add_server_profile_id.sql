ALTER TABLE topic_presets
ADD COLUMN IF NOT EXISTS broker_server_profile_id TEXT NULL;

UPDATE topic_presets
SET broker_server_profile_id = COALESCE(
    NULLIF(broker_server_profile_id, ''),
    (
        SELECT broker_server_profiles.id
        FROM broker_server_profiles
        WHERE broker_server_profiles.workspace_id = topic_presets.workspace_id
          AND CONCAT(broker_server_profiles.host, ':', broker_server_profiles.port) = topic_presets.broker_server
        ORDER BY broker_server_profiles.is_active DESC,
                 broker_server_profiles.created_at_utc DESC,
                 broker_server_profiles.id ASC
        LIMIT 1
    ))
WHERE broker_server_profile_id IS NULL OR broker_server_profile_id = '';

UPDATE topic_presets
SET broker_server_profile_id = (
    SELECT broker_server_profiles.id
    FROM broker_server_profiles
    WHERE broker_server_profiles.workspace_id = topic_presets.workspace_id
    ORDER BY broker_server_profiles.is_active DESC,
             broker_server_profiles.created_at_utc DESC,
             broker_server_profiles.id ASC
    LIMIT 1
)
WHERE broker_server_profile_id IS NULL OR broker_server_profile_id = '';

DROP INDEX IF EXISTS ux_topic_presets_workspace_broker_server_topic_pattern;

CREATE UNIQUE INDEX IF NOT EXISTS ux_topic_presets_workspace_profile_topic_pattern
    ON topic_presets(workspace_id, broker_server_profile_id, topic_pattern);

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_topic_presets_broker_server_profile') THEN
        ALTER TABLE topic_presets
            ADD CONSTRAINT fk_topic_presets_broker_server_profile
            FOREIGN KEY (broker_server_profile_id)
            REFERENCES broker_server_profiles(id)
            ON DELETE CASCADE;
    END IF;
END $$;
