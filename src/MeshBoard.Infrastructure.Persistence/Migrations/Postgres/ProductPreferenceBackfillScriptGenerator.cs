using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.Data.Sqlite;

namespace MeshBoard.Infrastructure.Persistence.Migrations.Postgres;

public sealed class ProductPreferenceBackfillScriptGenerator
{
    public async Task<string> GenerateFromSqliteAsync(
        string sqlitePathOrConnectionString,
        CancellationToken cancellationToken = default)
    {
        var connectionString = NormalizeSqliteConnectionString(sqlitePathOrConnectionString);

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var script = new StringBuilder();
        script.AppendLine("-- MeshBoard product preference backfill");
        script.AppendLine("-- Source: legacy SQLite preference/runtime database");
        script.AppendLine("BEGIN;");
        script.AppendLine();

        await AppendUsersAsync(script, connection, cancellationToken);
        await AppendBrokerServerProfilesAsync(script, connection, cancellationToken);
        await AppendFavoriteNodesAsync(script, connection, cancellationToken);
        await AppendTopicPresetsAsync(script, connection, cancellationToken);
        await AppendSavedChannelFiltersAsync(script, connection, cancellationToken);

        script.AppendLine("COMMIT;");

        return script.ToString();
    }

    private static async Task AppendUsersAsync(
        StringBuilder script,
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "users", cancellationToken))
        {
            return;
        }

        var rows = await connection.QueryAsync<UserRow>(
            new CommandDefinition(
                """
                SELECT id AS Id,
                       username AS Username,
                       normalized_username AS NormalizedUsername,
                       password_hash AS PasswordHash,
                       created_at_utc AS CreatedAtUtc
                FROM users
                ORDER BY id ASC;
                """,
                cancellationToken: cancellationToken));

        foreach (var row in rows)
        {
            script.AppendLine(
                $$"""
                INSERT INTO users (
                    id,
                    username,
                    normalized_username,
                    password_hash,
                    created_at_utc)
                VALUES (
                    {{SqlLiteral(row.Id)}},
                    {{SqlLiteral(row.Username)}},
                    {{SqlLiteral(row.NormalizedUsername)}},
                    {{SqlLiteral(row.PasswordHash)}},
                    {{SqlLiteral(row.CreatedAtUtc)}})
                ON CONFLICT (id) DO UPDATE SET
                    username = EXCLUDED.username,
                    normalized_username = EXCLUDED.normalized_username,
                    password_hash = EXCLUDED.password_hash,
                    created_at_utc = EXCLUDED.created_at_utc;
                """);
            script.AppendLine();
        }
    }

    private static async Task AppendBrokerServerProfilesAsync(
        StringBuilder script,
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "broker_server_profiles", cancellationToken))
        {
            return;
        }

        var rows = await connection.QueryAsync<BrokerServerProfileRow>(
            new CommandDefinition(
                """
                SELECT id AS Id,
                       workspace_id AS WorkspaceId,
                       name AS Name,
                       host AS Host,
                       port AS Port,
                       use_tls AS UseTls,
                       username AS Username,
                       password AS Password,
                       default_topic_pattern AS DefaultTopicPattern,
                       downlink_topic AS DownlinkTopic,
                       COALESCE(enable_send, 0) AS EnableSend,
                       COALESCE(is_active, 0) AS IsActive,
                       created_at_utc AS CreatedAtUtc
                FROM broker_server_profiles
                ORDER BY workspace_id ASC, id ASC;
                """,
                cancellationToken: cancellationToken));

        foreach (var row in rows)
        {
            script.AppendLine(
                $$"""
                INSERT INTO broker_server_profiles (
                    id,
                    workspace_id,
                    name,
                    host,
                    port,
                    use_tls,
                    username,
                    password,
                    default_topic_pattern,
                    downlink_topic,
                    enable_send,
                    is_active,
                    created_at_utc)
                VALUES (
                    {{SqlLiteral(row.Id)}},
                    {{SqlLiteral(row.WorkspaceId)}},
                    {{SqlLiteral(row.Name)}},
                    {{SqlLiteral(row.Host)}},
                    {{row.Port}},
                    {{row.UseTls}},
                    {{SqlLiteral(row.Username)}},
                    {{SqlLiteral(row.Password)}},
                    {{SqlLiteral(row.DefaultTopicPattern)}},
                    {{SqlLiteral(row.DownlinkTopic)}},
                    {{row.EnableSend}},
                    {{row.IsActive}},
                    {{SqlLiteral(row.CreatedAtUtc)}})
                ON CONFLICT (id) DO UPDATE SET
                    workspace_id = EXCLUDED.workspace_id,
                    name = EXCLUDED.name,
                    host = EXCLUDED.host,
                    port = EXCLUDED.port,
                    use_tls = EXCLUDED.use_tls,
                    username = EXCLUDED.username,
                    password = EXCLUDED.password,
                    default_topic_pattern = EXCLUDED.default_topic_pattern,
                    downlink_topic = EXCLUDED.downlink_topic,
                    enable_send = EXCLUDED.enable_send,
                    is_active = EXCLUDED.is_active,
                    created_at_utc = EXCLUDED.created_at_utc;
                """);
            script.AppendLine();
        }
    }

    private static async Task AppendFavoriteNodesAsync(
        StringBuilder script,
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "favorite_nodes", cancellationToken))
        {
            return;
        }

        var rows = await connection.QueryAsync<FavoriteNodeRow>(
            new CommandDefinition(
                """
                SELECT id AS Id,
                       workspace_id AS WorkspaceId,
                       node_id AS NodeId,
                       short_name AS ShortName,
                       long_name AS LongName,
                       created_at_utc AS CreatedAtUtc
                FROM favorite_nodes
                ORDER BY workspace_id ASC, node_id ASC;
                """,
                cancellationToken: cancellationToken));

        foreach (var row in rows)
        {
            script.AppendLine(
                $$"""
                INSERT INTO favorite_nodes (
                    id,
                    workspace_id,
                    node_id,
                    short_name,
                    long_name,
                    created_at_utc)
                VALUES (
                    {{SqlLiteral(row.Id)}},
                    {{SqlLiteral(row.WorkspaceId)}},
                    {{SqlLiteral(row.NodeId)}},
                    {{SqlLiteral(row.ShortName)}},
                    {{SqlLiteral(row.LongName)}},
                    {{SqlLiteral(row.CreatedAtUtc)}})
                ON CONFLICT (workspace_id, node_id) DO UPDATE SET
                    id = EXCLUDED.id,
                    short_name = EXCLUDED.short_name,
                    long_name = EXCLUDED.long_name,
                    created_at_utc = EXCLUDED.created_at_utc;
                """);
            script.AppendLine();
        }
    }

    private static async Task AppendTopicPresetsAsync(
        StringBuilder script,
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "topic_presets", cancellationToken))
        {
            return;
        }

        var topicPresetColumns = await GetColumnNamesAsync(connection, "topic_presets", cancellationToken);
        var brokerProfileLookups = await LoadBrokerServerProfileLookupsAsync(connection, cancellationToken);
        var rows = await connection.QueryAsync<TopicPresetRow>(
            new CommandDefinition(
                topicPresetColumns.Contains("broker_server_profile_id")
                    ? """
                      SELECT id AS Id,
                             workspace_id AS WorkspaceId,
                             broker_server_profile_id AS BrokerServerProfileId,
                             broker_server AS BrokerServer,
                             name AS Name,
                             topic_pattern AS TopicPattern,
                             COALESCE(is_default, 0) AS IsDefault,
                             created_at_utc AS CreatedAtUtc
                      FROM topic_presets
                      ORDER BY workspace_id ASC, broker_server ASC, topic_pattern ASC;
                      """
                    : """
                      SELECT id AS Id,
                             workspace_id AS WorkspaceId,
                             NULL AS BrokerServerProfileId,
                             broker_server AS BrokerServer,
                             name AS Name,
                             topic_pattern AS TopicPattern,
                             COALESCE(is_default, 0) AS IsDefault,
                             created_at_utc AS CreatedAtUtc
                      FROM topic_presets
                      ORDER BY workspace_id ASC, broker_server ASC, topic_pattern ASC;
                      """,
                cancellationToken: cancellationToken));

        foreach (var row in rows)
        {
            var brokerServerProfileId = ResolveTopicPresetBrokerServerProfileId(row, brokerProfileLookups)
                ?? throw new InvalidOperationException(
                    $"Unable to resolve a broker server profile for topic preset '{row.TopicPattern}' in workspace '{row.WorkspaceId}'.");

            script.AppendLine(
                $$"""
                INSERT INTO topic_presets (
                    id,
                    workspace_id,
                    broker_server_profile_id,
                    broker_server,
                    name,
                    topic_pattern,
                    is_default,
                    created_at_utc)
                VALUES (
                    {{SqlLiteral(row.Id)}},
                    {{SqlLiteral(row.WorkspaceId)}},
                    {{SqlLiteral(brokerServerProfileId)}},
                    {{SqlLiteral(row.BrokerServer)}},
                    {{SqlLiteral(row.Name)}},
                    {{SqlLiteral(row.TopicPattern)}},
                    {{row.IsDefault}},
                    {{SqlLiteral(row.CreatedAtUtc)}})
                ON CONFLICT (workspace_id, broker_server_profile_id, topic_pattern) DO UPDATE SET
                    id = EXCLUDED.id,
                    broker_server = EXCLUDED.broker_server,
                    name = EXCLUDED.name,
                    is_default = EXCLUDED.is_default,
                    created_at_utc = EXCLUDED.created_at_utc;
                """);
            script.AppendLine();
        }
    }

    private static async Task AppendSavedChannelFiltersAsync(
        StringBuilder script,
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<SavedChannelFilterRow> rows;

        if (await TableExistsAsync(connection, "saved_channel_filters", cancellationToken))
        {
            var savedRows = (await connection.QueryAsync<SavedChannelFilterRow>(
                new CommandDefinition(
                    """
                    SELECT id AS Id,
                           workspace_id AS WorkspaceId,
                           broker_server_profile_id AS BrokerServerProfileId,
                           topic_filter AS TopicFilter,
                           label AS Label,
                           created_at_utc AS CreatedAtUtc,
                           updated_at_utc AS UpdatedAtUtc
                    FROM saved_channel_filters
                    ORDER BY workspace_id ASC, broker_server_profile_id ASC, topic_filter ASC;
                    """,
                    cancellationToken: cancellationToken))).ToList();

            if (savedRows.Count > 0)
            {
                rows = savedRows;
            }
            else
            {
                rows = await LoadFallbackSavedChannelFiltersAsync(connection, cancellationToken);
            }
        }
        else
        {
            rows = await LoadFallbackSavedChannelFiltersAsync(connection, cancellationToken);
        }

        foreach (var row in rows)
        {
            script.AppendLine(
                $$"""
                INSERT INTO saved_channel_filters (
                    id,
                    workspace_id,
                    broker_server_profile_id,
                    topic_filter,
                    label,
                    created_at_utc,
                    updated_at_utc)
                VALUES (
                    {{SqlLiteral(row.Id)}},
                    {{SqlLiteral(row.WorkspaceId)}},
                    {{SqlLiteral(row.BrokerServerProfileId)}},
                    {{SqlLiteral(row.TopicFilter)}},
                    {{SqlLiteral(row.Label)}},
                    {{SqlLiteral(row.CreatedAtUtc)}},
                    {{SqlLiteral(row.UpdatedAtUtc)}})
                ON CONFLICT (workspace_id, broker_server_profile_id, topic_filter) DO UPDATE SET
                    id = EXCLUDED.id,
                    label = EXCLUDED.label,
                    created_at_utc = EXCLUDED.created_at_utc,
                    updated_at_utc = EXCLUDED.updated_at_utc;
                """);
            script.AppendLine();
        }
    }

    private static async Task<IReadOnlyCollection<SavedChannelFilterRow>> LoadFallbackSavedChannelFiltersAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "subscription_intents", cancellationToken))
        {
            return [];
        }

        var intents = await connection.QueryAsync<SubscriptionIntentRow>(
            new CommandDefinition(
                """
                SELECT workspace_id AS WorkspaceId,
                       broker_server_profile_id AS BrokerServerProfileId,
                       topic_filter AS TopicFilter,
                       created_at_utc AS CreatedAtUtc
                FROM subscription_intents
                ORDER BY workspace_id ASC, broker_server_profile_id ASC, topic_filter ASC;
                """,
                cancellationToken: cancellationToken));

        return intents
            .Select(
                intent => new SavedChannelFilterRow
                {
                    Id = CreateDeterministicGuid(intent.WorkspaceId, intent.BrokerServerProfileId, intent.TopicFilter),
                    WorkspaceId = intent.WorkspaceId,
                    BrokerServerProfileId = intent.BrokerServerProfileId,
                    TopicFilter = intent.TopicFilter,
                    Label = null,
                    CreatedAtUtc = intent.CreatedAtUtc,
                    UpdatedAtUtc = intent.CreatedAtUtc
                })
            .ToList();
    }

    private static async Task<bool> TableExistsAsync(
        SqliteConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        var result = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(
                """
                SELECT COUNT(*)
                FROM sqlite_master
                WHERE type = 'table'
                  AND name = @TableName;
                """,
                new { TableName = tableName },
                cancellationToken: cancellationToken));

        return result > 0;
    }

    private static async Task<HashSet<string>> GetColumnNamesAsync(
        SqliteConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        var columns = await connection.QueryAsync<TableInfoRow>(
            new CommandDefinition(
                $"PRAGMA table_info({tableName});",
                cancellationToken: cancellationToken));

        return columns
            .Select(column => column.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<Dictionary<string, List<BrokerServerProfileRow>>> LoadBrokerServerProfileLookupsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "broker_server_profiles", cancellationToken))
        {
            return new Dictionary<string, List<BrokerServerProfileRow>>(StringComparer.Ordinal);
        }

        var rows = await connection.QueryAsync<BrokerServerProfileRow>(
            new CommandDefinition(
                """
                SELECT id AS Id,
                       workspace_id AS WorkspaceId,
                       name AS Name,
                       host AS Host,
                       port AS Port,
                       use_tls AS UseTls,
                       username AS Username,
                       password AS Password,
                       default_topic_pattern AS DefaultTopicPattern,
                       downlink_topic AS DownlinkTopic,
                       COALESCE(enable_send, 0) AS EnableSend,
                       COALESCE(is_active, 0) AS IsActive,
                       created_at_utc AS CreatedAtUtc
                FROM broker_server_profiles
                ORDER BY workspace_id ASC, is_active DESC, created_at_utc DESC, id ASC;
                """,
                cancellationToken: cancellationToken));

        return rows
            .GroupBy(row => row.WorkspaceId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.ToList(),
                StringComparer.Ordinal);
    }

    private static string? ResolveTopicPresetBrokerServerProfileId(
        TopicPresetRow row,
        IReadOnlyDictionary<string, List<BrokerServerProfileRow>> brokerProfileLookups)
    {
        if (!string.IsNullOrWhiteSpace(row.BrokerServerProfileId))
        {
            return row.BrokerServerProfileId;
        }

        if (!brokerProfileLookups.TryGetValue(row.WorkspaceId, out var workspaceProfiles) || workspaceProfiles.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(row.BrokerServer))
        {
            var match = workspaceProfiles.FirstOrDefault(
                profile => string.Equals(profile.ServerAddress, row.BrokerServer, StringComparison.Ordinal));

            if (match is not null)
            {
                return match.Id;
            }
        }

        return workspaceProfiles[0].Id;
    }

    private static string NormalizeSqliteConnectionString(string sqlitePathOrConnectionString)
    {
        if (string.IsNullOrWhiteSpace(sqlitePathOrConnectionString))
        {
            throw new InvalidOperationException("A SQLite path or connection string is required.");
        }

        return sqlitePathOrConnectionString.Contains('=', StringComparison.Ordinal)
            ? sqlitePathOrConnectionString
            : $"Data Source={sqlitePathOrConnectionString.Trim()}";
    }

    private static string SqlLiteral(string? value)
    {
        return value is null
            ? "NULL"
            : $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
    }

    private static string CreateDeterministicGuid(string workspaceId, string brokerServerProfileId, string topicFilter)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes($"{workspaceId}|{brokerServerProfileId}|{topicFilter}"));
        return new Guid(hash).ToString();
    }

    private sealed class UserRow
    {
        public required string Id { get; set; }

        public required string Username { get; set; }

        public required string NormalizedUsername { get; set; }

        public required string PasswordHash { get; set; }

        public required string CreatedAtUtc { get; set; }
    }

    private sealed class BrokerServerProfileRow
    {
        public required string Id { get; set; }

        public required string WorkspaceId { get; set; }

        public required string Name { get; set; }

        public required string Host { get; set; }

        public int Port { get; set; }

        public int UseTls { get; set; }

        public string? Username { get; set; }

        public string? Password { get; set; }

        public required string DefaultTopicPattern { get; set; }

        public required string DownlinkTopic { get; set; }

        public int EnableSend { get; set; }

        public int IsActive { get; set; }

        public required string CreatedAtUtc { get; set; }

        public string ServerAddress => $"{Host}:{Port}";
    }

    private sealed class FavoriteNodeRow
    {
        public required string Id { get; set; }

        public required string WorkspaceId { get; set; }

        public required string NodeId { get; set; }

        public string? ShortName { get; set; }

        public string? LongName { get; set; }

        public required string CreatedAtUtc { get; set; }
    }

    private sealed class TopicPresetRow
    {
        public required string Id { get; set; }

        public required string WorkspaceId { get; set; }

        public string? BrokerServerProfileId { get; set; }

        public required string BrokerServer { get; set; }

        public required string Name { get; set; }

        public required string TopicPattern { get; set; }

        public int IsDefault { get; set; }

        public required string CreatedAtUtc { get; set; }
    }

    private sealed class SavedChannelFilterRow
    {
        public required string Id { get; set; }

        public required string WorkspaceId { get; set; }

        public required string BrokerServerProfileId { get; set; }

        public required string TopicFilter { get; set; }

        public string? Label { get; set; }

        public required string CreatedAtUtc { get; set; }

        public required string UpdatedAtUtc { get; set; }
    }

    private sealed class SubscriptionIntentRow
    {
        public required string WorkspaceId { get; set; }

        public required string BrokerServerProfileId { get; set; }

        public required string TopicFilter { get; set; }

        public required string CreatedAtUtc { get; set; }
    }

    private sealed class TableInfoRow
    {
        public required string Name { get; set; }
    }
}
