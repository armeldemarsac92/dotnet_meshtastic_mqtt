using Dapper;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Workspaces;
using MeshBoard.Infrastructure.Persistence.SQL;
using MeshBoard.Infrastructure.Persistence.SQL.Responses;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MeshBoard.Infrastructure.Persistence.Initialization;

internal sealed class SqliteDatabaseInitializer
{
    private readonly BrokerOptions _brokerOptions;
    private readonly ILogger<SqliteDatabaseInitializer> _logger;
    private readonly PersistenceOptions _persistenceOptions;

    public SqliteDatabaseInitializer(
        IOptions<BrokerOptions> brokerOptions,
        IOptions<PersistenceOptions> persistenceOptions,
        ILogger<SqliteDatabaseInitializer> logger)
    {
        _brokerOptions = brokerOptions.Value;
        _persistenceOptions = persistenceOptions.Value;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting to initialize the SQLite database");

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(
            new CommandDefinition(
                SchemaQueries.EnableWriteAheadLogging,
                cancellationToken: cancellationToken));

        await connection.ExecuteAsync(
            new CommandDefinition(
                SchemaQueries.SetSynchronousNormal,
                cancellationToken: cancellationToken));

        await connection.ExecuteAsync(
            new CommandDefinition(
                SchemaQueries.SetTempStoreMemory,
                cancellationToken: cancellationToken));

        var createSchemaCommand = new CommandDefinition(
            SchemaQueries.CreateSchema,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(createSchemaCommand);
        await MigrateMessageHistoryAsync(connection, cancellationToken);
        await MigrateNodesAsync(connection, cancellationToken);
        await MigrateFavoriteNodesAsync(connection, cancellationToken);
        await MigrateBrokerServerProfilesAsync(connection, cancellationToken);
        await MigrateDiscoveredTopicsAsync(connection, cancellationToken);
        await MigrateTopicPresetsAsync(connection, cancellationToken);
        await MigrateProjectionChangeLogAsync(connection, cancellationToken);

        var retentionCommand = new CommandDefinition(
            SchemaQueries.DeleteExpiredMessages,
            new
            {
                CutoffUtc = DateTimeOffset.UtcNow
                    .AddDays(-_persistenceOptions.MessageRetentionDays)
                    .ToString("O")
            },
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(retentionCommand);

        if (_persistenceOptions.SeedLegacyDefaultWorkspace)
        {
            var fallbackBrokerServer = $"{_brokerOptions.Host}:{_brokerOptions.Port}";
            await SeedTopicPresetAsync(
                connection,
                WorkspaceConstants.DefaultWorkspaceId,
                fallbackBrokerServer,
                "US Public Feed",
                _brokerOptions.DefaultTopicPattern,
                true,
                cancellationToken);
            await SeedTopicPresetAsync(
                connection,
                WorkspaceConstants.DefaultWorkspaceId,
                fallbackBrokerServer,
                "EU Public Feed",
                "msh/EU_433/2/e/#",
                false,
                cancellationToken);
            await SeedBrokerServerProfileAsync(connection, cancellationToken);
        }

        _logger.LogInformation("Initialized the SQLite database successfully");
    }

    private static async Task MigrateMessageHistoryAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var columnCommand = new CommandDefinition(
            SchemaQueries.GetMessageHistoryColumns,
            cancellationToken: cancellationToken);

        var columns = (await connection.QueryAsync<TableColumnSqlResponse>(columnCommand))
            .Select(column => column.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!columns.Contains("packet_type"))
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    SchemaQueries.AddMessageHistoryPacketTypeColumn,
                    cancellationToken: cancellationToken));
        }

        if (!columns.Contains("message_key"))
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    SchemaQueries.AddMessageHistoryMessageKeyColumn,
                    cancellationToken: cancellationToken));
        }

        if (!columns.Contains("broker_server"))
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    SchemaQueries.AddMessageHistoryBrokerServerColumn,
                    cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(
            new CommandDefinition(
                SchemaQueries.BackfillMessageHistoryPacketType,
                cancellationToken: cancellationToken));

        await connection.ExecuteAsync(
            new CommandDefinition(
                SchemaQueries.BackfillMessageHistoryMessageKey,
                cancellationToken: cancellationToken));

        await connection.ExecuteAsync(
            new CommandDefinition(
                SchemaQueries.BackfillMessageHistoryBrokerServer,
                cancellationToken: cancellationToken));

        await connection.ExecuteAsync(
            new CommandDefinition(
                SchemaQueries.CreateMessageHistoryMessageKeyIndex,
                cancellationToken: cancellationToken));

        await connection.ExecuteAsync(
            new CommandDefinition(
                SchemaQueries.CreateMessageHistoryBrokerServerReceivedAtIndex,
                cancellationToken: cancellationToken));
    }

    private static async Task MigrateNodesAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var columnCommand = new CommandDefinition(
            SchemaQueries.GetNodeColumns,
            cancellationToken: cancellationToken);

        var columns = (await connection.QueryAsync<TableColumnSqlResponse>(columnCommand))
            .Select(column => column.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        await EnsureColumnAsync(connection, columns, "battery_level_percent", SchemaQueries.AddNodesBatteryLevelPercentColumn, cancellationToken);
        await EnsureColumnAsync(connection, columns, "voltage", SchemaQueries.AddNodesVoltageColumn, cancellationToken);
        await EnsureColumnAsync(connection, columns, "channel_utilization", SchemaQueries.AddNodesChannelUtilizationColumn, cancellationToken);
        await EnsureColumnAsync(connection, columns, "air_util_tx", SchemaQueries.AddNodesAirUtilTxColumn, cancellationToken);
        await EnsureColumnAsync(connection, columns, "uptime_seconds", SchemaQueries.AddNodesUptimeSecondsColumn, cancellationToken);
        await EnsureColumnAsync(connection, columns, "temperature_celsius", SchemaQueries.AddNodesTemperatureCelsiusColumn, cancellationToken);
        await EnsureColumnAsync(connection, columns, "relative_humidity", SchemaQueries.AddNodesRelativeHumidityColumn, cancellationToken);
        await EnsureColumnAsync(connection, columns, "barometric_pressure", SchemaQueries.AddNodesBarometricPressureColumn, cancellationToken);
        await EnsureColumnAsync(connection, columns, "last_heard_channel", SchemaQueries.AddNodesLastHeardChannelColumn, cancellationToken);
        await EnsureColumnAsync(connection, columns, "broker_server", SchemaQueries.AddNodesBrokerServerColumn, cancellationToken);

        await connection.ExecuteAsync(
            new CommandDefinition(
                SchemaQueries.BackfillNodesBrokerServer,
                cancellationToken: cancellationToken));

        await connection.ExecuteAsync(
            new CommandDefinition(
                SchemaQueries.CreateNodesLastHeardChannelIndex,
                cancellationToken: cancellationToken));
    }

    private static async Task MigrateFavoriteNodesAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var columnCommand = new CommandDefinition(
            SchemaQueries.GetFavoriteNodeColumns,
            cancellationToken: cancellationToken);

        var columns = (await connection.QueryAsync<TableColumnSqlResponse>(columnCommand))
            .Select(column => column.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        await EnsureColumnAsync(
            connection,
            columns,
            "workspace_id",
            SchemaQueries.AddFavoriteNodesWorkspaceIdColumn,
            cancellationToken);

        await connection.ExecuteAsync(
            new CommandDefinition(
                SchemaQueries.BackfillFavoriteNodesWorkspaceId,
                new
                {
                    WorkspaceId = WorkspaceConstants.DefaultWorkspaceId
                },
                cancellationToken: cancellationToken));

        await connection.ExecuteAsync(
            new CommandDefinition(
                SchemaQueries.DropFavoriteNodesLegacyNodeIdIndex,
                cancellationToken: cancellationToken));

        await connection.ExecuteAsync(
            new CommandDefinition(
                SchemaQueries.CreateFavoriteNodesWorkspaceNodeIdIndex,
                cancellationToken: cancellationToken));
    }

    private static async Task MigrateProjectionChangeLogAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var columnCommand = new CommandDefinition(
            SchemaQueries.GetProjectionChangeLogColumns,
            cancellationToken: cancellationToken);

        var columns = (await connection.QueryAsync<TableColumnSqlResponse>(columnCommand))
            .Select(column => column.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        await EnsureColumnAsync(
            connection,
            columns,
            "entity_key",
            SchemaQueries.AddProjectionChangeLogEntityKeyColumn,
            cancellationToken);
    }

    private static async Task MigrateBrokerServerProfilesAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var columnCommand = new CommandDefinition(
            SchemaQueries.GetBrokerServerProfileColumns,
            cancellationToken: cancellationToken);

        var columns = (await connection.QueryAsync<TableColumnSqlResponse>(columnCommand))
            .Select(column => column.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        await EnsureColumnAsync(
            connection,
            columns,
            "workspace_id",
            SchemaQueries.AddBrokerServerProfilesWorkspaceIdColumn,
            cancellationToken);

        await EnsureColumnAsync(
            connection,
            columns,
            "subscription_intents_initialized",
            SchemaQueries.AddBrokerServerProfilesSubscriptionIntentsInitializedColumn,
            cancellationToken);

        await connection.ExecuteAsync(
            new CommandDefinition(
                SchemaQueries.BackfillBrokerServerProfilesWorkspaceId,
                new
                {
                    WorkspaceId = WorkspaceConstants.DefaultWorkspaceId
                },
                cancellationToken: cancellationToken));

        await connection.ExecuteAsync(
            new CommandDefinition(
                SchemaQueries.BackfillBrokerServerProfilesSubscriptionIntentsInitialized,
                cancellationToken: cancellationToken));

        await connection.ExecuteAsync(
            new CommandDefinition(
                SchemaQueries.DropBrokerServerProfilesLegacyNameIndex,
                cancellationToken: cancellationToken));

        await connection.ExecuteAsync(
            new CommandDefinition(
                SchemaQueries.CreateBrokerServerProfilesWorkspaceNameIndex,
                cancellationToken: cancellationToken));
    }

    private async Task MigrateDiscoveredTopicsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var fallbackBrokerServer = $"{_brokerOptions.Host}:{_brokerOptions.Port}";
        var columnCommand = new CommandDefinition(
            SchemaQueries.GetDiscoveredTopicColumns,
            cancellationToken: cancellationToken);

        var columns = (await connection.QueryAsync<TableColumnSqlResponse>(columnCommand)).ToList();
        var columnNames = columns
            .Select(column => column.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var hasWorkspaceIdColumn = columnNames.Contains("workspace_id");
        var hasBrokerServerColumn = columnNames.Contains("broker_server");
        var hasCompositePrimaryKey =
            columns.Any(column => string.Equals(column.Name, "workspace_id", StringComparison.OrdinalIgnoreCase) && column.Pk > 0) &&
            columns.Any(column => string.Equals(column.Name, "broker_server", StringComparison.OrdinalIgnoreCase) && column.Pk > 0) &&
            columns.Any(column => string.Equals(column.Name, "topic_pattern", StringComparison.OrdinalIgnoreCase) && column.Pk > 0);

        if (hasWorkspaceIdColumn && hasBrokerServerColumn && hasCompositePrimaryKey)
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    SchemaQueries.CreateDiscoveredTopicsLastObservedIndex,
                    cancellationToken: cancellationToken));
            return;
        }

        await connection.ExecuteAsync(
            new CommandDefinition(
                SchemaQueries.DropDiscoveredTopicsLegacyTable,
                cancellationToken: cancellationToken));

        await connection.ExecuteAsync(
            new CommandDefinition(
                SchemaQueries.RenameDiscoveredTopicsToLegacy,
                cancellationToken: cancellationToken));

        await connection.ExecuteAsync(
            new CommandDefinition(
                SchemaQueries.RecreateDiscoveredTopicsWithBrokerServer,
                cancellationToken: cancellationToken));

        var copyQuery = hasBrokerServerColumn
            ? SchemaQueries.CopyDiscoveredTopicsFromLegacyWithBrokerServer
            : SchemaQueries.CopyDiscoveredTopicsFromLegacyWithoutBrokerServer;

        await connection.ExecuteAsync(
            new CommandDefinition(
                copyQuery,
                new
                {
                    WorkspaceId = WorkspaceConstants.DefaultWorkspaceId,
                    BrokerServer = fallbackBrokerServer
                },
                cancellationToken: cancellationToken));

        await connection.ExecuteAsync(
            new CommandDefinition(
                SchemaQueries.DropDiscoveredTopicsLegacyTable,
                cancellationToken: cancellationToken));

        await connection.ExecuteAsync(
            new CommandDefinition(
                SchemaQueries.CreateDiscoveredTopicsLastObservedIndex,
                cancellationToken: cancellationToken));
    }

    private async Task MigrateTopicPresetsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var fallbackBrokerServer = $"{_brokerOptions.Host}:{_brokerOptions.Port}";
        var columnCommand = new CommandDefinition(
            SchemaQueries.GetTopicPresetColumns,
            cancellationToken: cancellationToken);

        var columns = (await connection.QueryAsync<TableColumnSqlResponse>(columnCommand))
            .Select(column => column.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        await EnsureColumnAsync(
            connection,
            columns,
            "encryption_key_base64",
            SchemaQueries.AddTopicPresetsEncryptionKeyBase64Column,
            cancellationToken);

        await EnsureColumnAsync(
            connection,
            columns,
            "broker_server",
            SchemaQueries.AddTopicPresetsBrokerServerColumn,
            cancellationToken);

        await EnsureColumnAsync(
            connection,
            columns,
            "workspace_id",
            SchemaQueries.AddTopicPresetsWorkspaceIdColumn,
            cancellationToken);

        await connection.ExecuteAsync(
            new CommandDefinition(
                SchemaQueries.BackfillTopicPresetsBrokerServer,
                new
                {
                    BrokerServer = fallbackBrokerServer
                },
                cancellationToken: cancellationToken));

        await connection.ExecuteAsync(
            new CommandDefinition(
                SchemaQueries.BackfillTopicPresetsWorkspaceId,
                new
                {
                    WorkspaceId = WorkspaceConstants.DefaultWorkspaceId
                },
                cancellationToken: cancellationToken));

        await connection.ExecuteAsync(
            new CommandDefinition(
                SchemaQueries.DropTopicPresetsLegacyTopicPatternIndex,
                cancellationToken: cancellationToken));

        await connection.ExecuteAsync(
            new CommandDefinition(
                SchemaQueries.DropTopicPresetsLegacyBrokerServerTopicPatternIndex,
                cancellationToken: cancellationToken));

        await connection.ExecuteAsync(
            new CommandDefinition(
                SchemaQueries.CreateTopicPresetsWorkspaceBrokerServerTopicPatternIndex,
                cancellationToken: cancellationToken));
    }

    private static Task EnsureColumnAsync(
        SqliteConnection connection,
        ISet<string> columns,
        string columnName,
        string sql,
        CancellationToken cancellationToken)
    {
        if (columns.Contains(columnName))
        {
            return Task.CompletedTask;
        }

        return connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    private SqliteConnection CreateConnection()
    {
        if (string.IsNullOrWhiteSpace(_persistenceOptions.ConnectionString))
        {
            throw new InvalidOperationException("The persistence connection string is not configured.");
        }

        return new SqliteConnection(_persistenceOptions.ConnectionString);
    }

    private static Task SeedTopicPresetAsync(
        SqliteConnection connection,
        string workspaceId,
        string brokerServer,
        string name,
        string topicPattern,
        bool isDefault,
        CancellationToken cancellationToken)
    {
        var command = new CommandDefinition(
            TopicPresetQueries.InsertTopicPresetIfMissing,
            new
            {
                Id = Guid.NewGuid().ToString(),
                WorkspaceId = workspaceId,
                BrokerServer = brokerServer,
                Name = name,
                TopicPattern = topicPattern,
                EncryptionKeyBase64 = (string?)null,
                IsDefault = isDefault ? 1 : 0,
                CreatedAtUtc = DateTimeOffset.UtcNow.ToString("O")
            },
            cancellationToken: cancellationToken);

        return connection.ExecuteAsync(command);
    }

    private Task SeedBrokerServerProfileAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var normalizedDefaultKey = Contracts.Topics.TopicEncryptionKey.NormalizeToBase64OrNull(_brokerOptions.DefaultEncryptionKeyBase64) ??
            Contracts.Topics.TopicEncryptionKey.DefaultKeyBase64;

        var command = new CommandDefinition(
            BrokerServerProfileQueries.InsertIfNoProfilesExist,
            new
            {
                Id = Guid.NewGuid().ToString(),
                WorkspaceId = WorkspaceConstants.DefaultWorkspaceId,
                Name = "Default server",
                Host = _brokerOptions.Host,
                Port = _brokerOptions.Port,
                UseTls = _brokerOptions.UseTls ? 1 : 0,
                Username = _brokerOptions.Username,
                Password = _brokerOptions.Password,
                DefaultTopicPattern = _brokerOptions.DefaultTopicPattern,
                DefaultEncryptionKeyBase64 = normalizedDefaultKey,
                DownlinkTopic = _brokerOptions.DownlinkTopic,
                EnableSend = _brokerOptions.EnableSend ? 1 : 0,
                CreatedAtUtc = DateTimeOffset.UtcNow.ToString("O")
            },
            cancellationToken: cancellationToken);

        return connection.ExecuteAsync(command);
    }
}
