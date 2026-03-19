using MeshBoard.Infrastructure.Persistence.Migrations.Postgres;
using Microsoft.Data.Sqlite;

namespace MeshBoard.IntegrationTests;

public sealed class ProductPreferenceBackfillScriptGeneratorTests
{
    [Fact]
    public async Task GenerateFromSqliteAsync_ShouldEmitRetainedPreferenceBackfill_WithoutSecretColumns()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"meshboard-backfill-{Guid.NewGuid():N}.db");

        try
        {
            await using var connection = new SqliteConnection($"Data Source={databasePath}");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE users (
                    id TEXT NOT NULL PRIMARY KEY,
                    username TEXT NOT NULL,
                    normalized_username TEXT NOT NULL,
                    password_hash TEXT NOT NULL,
                    created_at_utc TEXT NOT NULL
                );

                CREATE TABLE broker_server_profiles (
                    id TEXT NOT NULL PRIMARY KEY,
                    workspace_id TEXT NOT NULL,
                    name TEXT NOT NULL,
                    host TEXT NOT NULL,
                    port INTEGER NOT NULL,
                    use_tls INTEGER NOT NULL,
                    username TEXT NULL,
                    password TEXT NULL,
                    default_topic_pattern TEXT NOT NULL,
                    default_encryption_key_base64 TEXT NULL,
                    downlink_topic TEXT NOT NULL,
                    enable_send INTEGER NOT NULL,
                    subscription_intents_initialized INTEGER NOT NULL DEFAULT 0,
                    is_active INTEGER NOT NULL,
                    created_at_utc TEXT NOT NULL
                );

                CREATE TABLE favorite_nodes (
                    id TEXT NOT NULL PRIMARY KEY,
                    workspace_id TEXT NOT NULL,
                    node_id TEXT NOT NULL,
                    short_name TEXT NULL,
                    long_name TEXT NULL,
                    created_at_utc TEXT NOT NULL
                );

                CREATE TABLE topic_presets (
                    id TEXT NOT NULL PRIMARY KEY,
                    workspace_id TEXT NOT NULL,
                    broker_server TEXT NOT NULL,
                    name TEXT NOT NULL,
                    topic_pattern TEXT NOT NULL,
                    encryption_key_base64 TEXT NULL,
                    is_default INTEGER NOT NULL,
                    created_at_utc TEXT NOT NULL
                );

                CREATE TABLE saved_channel_filters (
                    id TEXT NOT NULL PRIMARY KEY,
                    workspace_id TEXT NOT NULL,
                    broker_server_profile_id TEXT NOT NULL,
                    topic_filter TEXT NOT NULL,
                    label TEXT NULL,
                    created_at_utc TEXT NOT NULL,
                    updated_at_utc TEXT NOT NULL
                );
                """;
            await command.ExecuteNonQueryAsync();

            await InsertRowAsync(
                connection,
                """
                INSERT INTO users (id, username, normalized_username, password_hash, created_at_utc)
                VALUES ('workspace-a', 'alice', 'ALICE', 'hash-a', '2026-03-19T08:00:00.0000000+00:00');
                """);
            await InsertRowAsync(
                connection,
                """
                INSERT INTO broker_server_profiles (
                    id, workspace_id, name, host, port, use_tls, username, password, default_topic_pattern,
                    default_encryption_key_base64, downlink_topic, enable_send, subscription_intents_initialized,
                    is_active, created_at_utc)
                VALUES (
                    '11111111-1111-1111-1111-111111111111',
                    'workspace-a',
                    'Alice''s Broker',
                    'mqtt.example.org',
                    1883,
                    1,
                    'alice-user',
                    'secret',
                    'msh/US/2/e/#',
                    'AQIDBAUGBwgJCgsMDQ4PEA==',
                    'msh/US/2/json/mqtt/',
                    1,
                    1,
                    1,
                    '2026-03-19T08:05:00.0000000+00:00');
                """);
            await InsertRowAsync(
                connection,
                """
                INSERT INTO favorite_nodes (id, workspace_id, node_id, short_name, long_name, created_at_utc)
                VALUES ('22222222-2222-2222-2222-222222222222', 'workspace-a', '!abc123', 'ALC', 'Alice Node', '2026-03-19T08:06:00.0000000+00:00');
                """);
            await InsertRowAsync(
                connection,
                """
                INSERT INTO topic_presets (id, workspace_id, broker_server, name, topic_pattern, encryption_key_base64, is_default, created_at_utc)
                VALUES ('33333333-3333-3333-3333-333333333333', 'workspace-a', 'mqtt.example.org:1883', 'Primary', 'msh/US/2/e/#', 'AQ==', 1, '2026-03-19T08:07:00.0000000+00:00');
                """);
            await InsertRowAsync(
                connection,
                """
                INSERT INTO saved_channel_filters (id, workspace_id, broker_server_profile_id, topic_filter, label, created_at_utc, updated_at_utc)
                VALUES ('44444444-4444-4444-4444-444444444444', 'workspace-a', '11111111-1111-1111-1111-111111111111', 'msh/US/2/e/LongFast/#', 'Portable feed', '2026-03-19T08:08:00.0000000+00:00', '2026-03-19T08:09:00.0000000+00:00');
                """);

            var generator = new ProductPreferenceBackfillScriptGenerator();
            var script = await generator.GenerateFromSqliteAsync(databasePath);

            Assert.Contains("INSERT INTO users", script, StringComparison.Ordinal);
            Assert.Contains("INSERT INTO broker_server_profiles", script, StringComparison.Ordinal);
            Assert.Contains("INSERT INTO favorite_nodes", script, StringComparison.Ordinal);
            Assert.Contains("INSERT INTO topic_presets", script, StringComparison.Ordinal);
            Assert.Contains("INSERT INTO saved_channel_filters", script, StringComparison.Ordinal);
            Assert.Contains("Alice''s Broker", script, StringComparison.Ordinal);
            Assert.DoesNotContain("default_encryption_key_base64", script, StringComparison.Ordinal);
            Assert.DoesNotContain("encryption_key_base64", script, StringComparison.Ordinal);
            Assert.DoesNotContain("AQIDBAUGBwgJCgsMDQ4PEA==", script, StringComparison.Ordinal);
            Assert.DoesNotContain("AQ==", script, StringComparison.Ordinal);
        }
        finally
        {
            TryDelete(databasePath);
        }
    }

    [Fact]
    public async Task GenerateFromSqliteAsync_ShouldFallbackToSubscriptionIntents_WhenSavedChannelFiltersAreUnavailable()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"meshboard-backfill-{Guid.NewGuid():N}.db");

        try
        {
            await using var connection = new SqliteConnection($"Data Source={databasePath}");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE subscription_intents (
                    workspace_id TEXT NOT NULL,
                    broker_server_profile_id TEXT NOT NULL,
                    topic_filter TEXT NOT NULL,
                    created_at_utc TEXT NOT NULL
                );
                """;
            await command.ExecuteNonQueryAsync();

            await InsertRowAsync(
                connection,
                """
                INSERT INTO subscription_intents (workspace_id, broker_server_profile_id, topic_filter, created_at_utc)
                VALUES ('workspace-a', '11111111-1111-1111-1111-111111111111', 'msh/US/2/e/LongFast/#', '2026-03-19T08:10:00.0000000+00:00');
                """);

            var generator = new ProductPreferenceBackfillScriptGenerator();
            var script = await generator.GenerateFromSqliteAsync(databasePath);

            Assert.Contains("INSERT INTO saved_channel_filters", script, StringComparison.Ordinal);
            Assert.Contains("msh/US/2/e/LongFast/#", script, StringComparison.Ordinal);
            Assert.DoesNotContain("INSERT INTO subscription_intents", script, StringComparison.Ordinal);
        }
        finally
        {
            TryDelete(databasePath);
        }
    }

    private static async Task InsertRowAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static void TryDelete(string databasePath)
    {
        try
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
        catch
        {
        }
    }
}
