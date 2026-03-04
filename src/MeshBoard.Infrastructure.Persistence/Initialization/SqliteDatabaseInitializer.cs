using Dapper;
using MeshBoard.Contracts.Configuration;
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

        var createSchemaCommand = new CommandDefinition(
            SchemaQueries.CreateSchema,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(createSchemaCommand);
        await MigrateMessageHistoryAsync(connection, cancellationToken);
        await MigrateNodesAsync(connection, cancellationToken);

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

        await SeedTopicPresetAsync(connection, "US Public Feed", _brokerOptions.DefaultTopicPattern, true, cancellationToken);
        await SeedTopicPresetAsync(connection, "EU Public Feed", "msh/EU_433/2/e/#", false, cancellationToken);

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
                SchemaQueries.CreateMessageHistoryMessageKeyIndex,
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
                Name = name,
                TopicPattern = topicPattern,
                IsDefault = isDefault ? 1 : 0,
                CreatedAtUtc = DateTimeOffset.UtcNow.ToString("O")
            },
            cancellationToken: cancellationToken);

        return connection.ExecuteAsync(command);
    }
}
