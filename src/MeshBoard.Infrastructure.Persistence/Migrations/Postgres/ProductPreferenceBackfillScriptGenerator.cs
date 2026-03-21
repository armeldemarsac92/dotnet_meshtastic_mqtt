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

        public required string DownlinkTopic { get; set; }

        public int EnableSend { get; set; }

        public int IsActive { get; set; }

        public required string CreatedAtUtc { get; set; }

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
}
