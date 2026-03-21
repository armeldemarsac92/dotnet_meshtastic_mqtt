-- Remove topic_presets and saved_channel_filters tables
DROP TABLE IF EXISTS topic_presets;
DROP TABLE IF EXISTS saved_channel_filters;

-- Remove topic-related columns from broker_server_profiles
-- (SQLite doesn't support DROP COLUMN easily, but Postgres does)
ALTER TABLE broker_server_profiles DROP COLUMN IF EXISTS default_topic_pattern;
ALTER TABLE broker_server_profiles DROP COLUMN IF EXISTS default_encryption_key_base64;
