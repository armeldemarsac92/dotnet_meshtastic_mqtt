using Dapper;
using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Meshtastic;
using MeshBoard.Infrastructure.Persistence.SQL;
using MeshBoard.Infrastructure.Persistence.SQL.Responses;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace MeshBoard.Infrastructure.Persistence.Runtime;

internal sealed class SqliteBrokerRuntimeCommandRepository : IBrokerRuntimeCommandRepository
{
    private const string StatusPending = "pending";
    private const string StatusLeased = "leased";
    private const string StatusCompleted = "completed";
    private const string StatusFailed = "failed";

    private readonly string _connectionString;

    public SqliteBrokerRuntimeCommandRepository(IOptions<PersistenceOptions> persistenceOptions)
    {
        var options = persistenceOptions.Value;

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException("The persistence connection string is not configured.");
        }

        _connectionString = options.ConnectionString;
    }

    public async Task EnqueueAsync(BrokerRuntimeCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(
            new CommandDefinition(
                BrokerRuntimeCommandQueries.Insert,
                new
                {
                    Id = command.Id.ToString(),
                    WorkspaceId = command.WorkspaceId,
                    CommandType = command.CommandType.ToString(),
                    command.Topic,
                    command.Payload,
                    command.TopicFilter,
                    Status = ToStorageStatus(command.Status == default ? BrokerRuntimeCommandStatus.Pending : command.Status),
                    command.AttemptCount,
                    CreatedAtUtc = command.CreatedAtUtc.ToString("O"),
                    AvailableAtUtc = command.AvailableAtUtc.ToString("O")
                },
                cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyCollection<BrokerRuntimeCommand>> GetRecentAsync(
        string workspaceId,
        int take,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceId);

        if (take <= 0)
        {
            return [];
        }

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var responses = await connection.QueryAsync<BrokerRuntimeCommandSqlResponse>(
            new CommandDefinition(
                BrokerRuntimeCommandQueries.GetRecentByWorkspace,
                new
                {
                    WorkspaceId = workspaceId,
                    Take = take
                },
                cancellationToken: cancellationToken));

        return responses.Select(Map).ToList();
    }

    public async Task<IReadOnlyCollection<BrokerRuntimeCommand>> LeasePendingAsync(
        string processorId,
        int batchSize,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processorId);

        if (batchSize <= 0)
        {
            return [];
        }

        var utcNow = DateTimeOffset.UtcNow;
        var leaseExpiresAtUtc = utcNow.Add(leaseDuration);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var ids = (await connection.QueryAsync<string>(
            new CommandDefinition(
                BrokerRuntimeCommandQueries.SelectLeaseableIds,
                new
                {
                    PendingStatus = StatusPending,
                    LeasedStatus = StatusLeased,
                    NowUtc = utcNow.ToString("O"),
                    BatchSize = batchSize
                },
                transaction,
                cancellationToken: cancellationToken))).ToList();

        if (ids.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return [];
        }

        await connection.ExecuteAsync(
            new CommandDefinition(
                BrokerRuntimeCommandQueries.LeaseByIds,
                new
                {
                    Ids = ids,
                    LeasedStatus = StatusLeased,
                    LeasedBy = processorId,
                    LeasedAtUtc = utcNow.ToString("O"),
                    LeaseExpiresAtUtc = leaseExpiresAtUtc.ToString("O")
                },
                transaction,
                cancellationToken: cancellationToken));

        var responses = (await connection.QueryAsync<BrokerRuntimeCommandSqlResponse>(
            new CommandDefinition(
                BrokerRuntimeCommandQueries.GetByIds,
                new { Ids = ids },
                transaction,
                cancellationToken: cancellationToken))).ToList();

        await transaction.CommitAsync(cancellationToken);

        return responses.Select(Map).ToList();
    }

    public async Task MarkCompletedAsync(Guid commandId, CancellationToken cancellationToken = default)
    {
        await UpdateStatusAsync(
            BrokerRuntimeCommandQueries.MarkCompleted,
            new
            {
                Id = commandId.ToString(),
                CompletedStatus = StatusCompleted,
                CompletedAtUtc = DateTimeOffset.UtcNow.ToString("O")
            },
            cancellationToken);
    }

    public async Task MarkPendingAsync(
        Guid commandId,
        DateTimeOffset availableAtUtc,
        string? lastError,
        CancellationToken cancellationToken = default)
    {
        await UpdateStatusAsync(
            BrokerRuntimeCommandQueries.MarkPending,
            new
            {
                Id = commandId.ToString(),
                PendingStatus = StatusPending,
                AvailableAtUtc = availableAtUtc.ToString("O"),
                LastError = lastError
            },
            cancellationToken);
    }

    public async Task MarkFailedAsync(
        Guid commandId,
        string? lastError,
        CancellationToken cancellationToken = default)
    {
        await UpdateStatusAsync(
            BrokerRuntimeCommandQueries.MarkFailed,
            new
            {
                Id = commandId.ToString(),
                FailedStatus = StatusFailed,
                FailedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                LastError = lastError
            },
            cancellationToken);
    }

    private async Task UpdateStatusAsync(string sql, object parameters, CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    private SqliteConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }

    private static BrokerRuntimeCommand Map(BrokerRuntimeCommandSqlResponse response)
    {
        return new BrokerRuntimeCommand
        {
            Id = Guid.Parse(response.Id),
            WorkspaceId = response.WorkspaceId,
            CommandType = Enum.Parse<BrokerRuntimeCommandType>(response.CommandType, ignoreCase: true),
            Status = ParseStatus(response.Status),
            Topic = response.Topic,
            Payload = response.Payload,
            TopicFilter = response.TopicFilter,
            AttemptCount = response.AttemptCount,
            CreatedAtUtc = DateTimeOffset.Parse(response.CreatedAtUtc),
            AvailableAtUtc = DateTimeOffset.Parse(response.AvailableAtUtc),
            LeasedAtUtc = string.IsNullOrWhiteSpace(response.LeasedAtUtc) ? null : DateTimeOffset.Parse(response.LeasedAtUtc),
            LeaseExpiresAtUtc = string.IsNullOrWhiteSpace(response.LeaseExpiresAtUtc) ? null : DateTimeOffset.Parse(response.LeaseExpiresAtUtc),
            CompletedAtUtc = string.IsNullOrWhiteSpace(response.CompletedAtUtc) ? null : DateTimeOffset.Parse(response.CompletedAtUtc),
            FailedAtUtc = string.IsNullOrWhiteSpace(response.FailedAtUtc) ? null : DateTimeOffset.Parse(response.FailedAtUtc),
            LastError = response.LastError
        };
    }

    private static BrokerRuntimeCommandStatus ParseStatus(string status)
    {
        return status switch
        {
            StatusPending => BrokerRuntimeCommandStatus.Pending,
            StatusLeased => BrokerRuntimeCommandStatus.Leased,
            StatusCompleted => BrokerRuntimeCommandStatus.Completed,
            StatusFailed => BrokerRuntimeCommandStatus.Failed,
            _ => throw new InvalidOperationException($"Unsupported broker runtime command status '{status}'.")
        };
    }

    private static string ToStorageStatus(BrokerRuntimeCommandStatus status)
    {
        return status switch
        {
            BrokerRuntimeCommandStatus.Pending => StatusPending,
            BrokerRuntimeCommandStatus.Leased => StatusLeased,
            BrokerRuntimeCommandStatus.Completed => StatusCompleted,
            BrokerRuntimeCommandStatus.Failed => StatusFailed,
            _ => throw new InvalidOperationException($"Unsupported broker runtime command status '{status}'.")
        };
    }
}
