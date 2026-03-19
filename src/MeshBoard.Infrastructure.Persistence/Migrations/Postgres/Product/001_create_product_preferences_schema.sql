CREATE TABLE IF NOT EXISTS users (
    id TEXT NOT NULL PRIMARY KEY,
    username TEXT NOT NULL,
    normalized_username TEXT NOT NULL,
    password_hash TEXT NOT NULL,
    created_at_utc TEXT NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_users_normalized_username
    ON users(normalized_username);

CREATE TABLE IF NOT EXISTS broker_server_profiles (
    id TEXT NOT NULL PRIMARY KEY,
    workspace_id TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    name TEXT NOT NULL,
    host TEXT NOT NULL,
    port INTEGER NOT NULL,
    use_tls INTEGER NOT NULL,
    username TEXT NULL,
    password TEXT NULL,
    default_topic_pattern TEXT NOT NULL,
    downlink_topic TEXT NOT NULL,
    enable_send INTEGER NOT NULL,
    subscription_intents_initialized INTEGER NOT NULL DEFAULT 0,
    is_active INTEGER NOT NULL,
    created_at_utc TEXT NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_broker_server_profiles_workspace_name
    ON broker_server_profiles(workspace_id, name);

CREATE TABLE IF NOT EXISTS favorite_nodes (
    id TEXT NOT NULL PRIMARY KEY,
    workspace_id TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    node_id TEXT NOT NULL,
    short_name TEXT NULL,
    long_name TEXT NULL,
    created_at_utc TEXT NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_favorite_nodes_workspace_node_id
    ON favorite_nodes(workspace_id, node_id);

CREATE TABLE IF NOT EXISTS topic_presets (
    id TEXT NOT NULL PRIMARY KEY,
    workspace_id TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    broker_server TEXT NOT NULL,
    name TEXT NOT NULL,
    topic_pattern TEXT NOT NULL,
    is_default INTEGER NOT NULL,
    created_at_utc TEXT NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_topic_presets_workspace_broker_server_topic_pattern
    ON topic_presets(workspace_id, broker_server, topic_pattern);

CREATE TABLE IF NOT EXISTS saved_channel_filters (
    id TEXT NOT NULL PRIMARY KEY,
    workspace_id TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    broker_server_profile_id TEXT NOT NULL REFERENCES broker_server_profiles(id) ON DELETE CASCADE,
    topic_filter TEXT NOT NULL,
    label TEXT NULL,
    created_at_utc TEXT NOT NULL,
    updated_at_utc TEXT NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_saved_channel_filters_workspace_profile_topic_filter
    ON saved_channel_filters(workspace_id, broker_server_profile_id, topic_filter);
