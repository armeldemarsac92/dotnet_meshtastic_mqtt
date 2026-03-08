using Dapper;
using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Realtime;
using MeshBoard.Infrastructure.Persistence.SQL;
using MeshBoard.Infrastructure.Persistence.SQL.Responses;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MeshBoard.Infrastructure.Persistence.Runtime;

internal sealed class SqliteProjectionChangeRepository : IProjectionChangeRepository
{
    private readonly string _connectionString;
    private readonly ILogger<SqliteProjectionChangeRepository> _logger;

    public SqliteProjectionChangeRepository(
        IOptions<PersistenceOptions> persistenceOptions,
        ILogger<SqliteProjectionChangeRepository> logger)
    {
        var persistence = persistenceOptions.Value;

        if (string.IsNullOrWhiteSpace(persistence.ConnectionString))
        {
            throw new InvalidOperationException("The persistence connection string is not configured.");
        }

        _connectionString = persistence.ConnectionString;
        _logger = logger;
    }

    public async Task AppendAsync(
        string workspaceId,
        IReadOnlyCollection<ProjectionChangeDescriptor> changes,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceId);
        ArgumentNullException.ThrowIfNull(changes);

        var normalizedChanges = changes
            .Where(change => change is not null)
            .Select(
                change => new
                {
                    change.Kind,
                    EntityKey = string.IsNullOrWhiteSpace(change.EntityKey)
                        ? null
                        : change.EntityKey.Trim()
                })
            .Distinct()
            .ToArray();

        if (normalizedChanges.Length == 0)
        {
            return;
        }

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var occurredAtUtc = DateTimeOffset.UtcNow.ToString("O");
        var rows = normalizedChanges.Select(
            change => new
            {
                WorkspaceId = workspaceId.Trim(),
                ChangeKind = change.Kind.ToString(),
                change.EntityKey,
                OccurredAtUtc = occurredAtUtc
            });

        await connection.ExecuteAsync(
            new CommandDefinition(
                ProjectionChangeQueries.Insert,
                rows,
                transaction,
                cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ProjectionChangeEvent>> GetChangesAfterAsync(
        long lastSeenId,
        int take,
        CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var responses = await connection.QueryAsync<ProjectionChangeSqlResponse>(
            new CommandDefinition(
                ProjectionChangeQueries.GetChangesAfter,
                new
                {
                    LastSeenId = lastSeenId,
                    Take = take
                },
                cancellationToken: cancellationToken));

        return responses
            .Select(Map)
            .Where(change => change is not null)
            .Cast<ProjectionChangeEvent>()
            .ToList();
    }

    public async Task<long> GetLatestIdAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        return await connection.QueryFirstAsync<long>(
            new CommandDefinition(
                ProjectionChangeQueries.GetLatestId,
                cancellationToken: cancellationToken));
    }

    public async Task<int> DeleteOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        return await connection.ExecuteAsync(
            new CommandDefinition(
                ProjectionChangeQueries.DeleteOlderThan,
                new { CutoffUtc = cutoffUtc.ToString("O") },
                cancellationToken: cancellationToken));
    }

    private SqliteConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }

    private ProjectionChangeEvent? Map(ProjectionChangeSqlResponse response)
    {
        if (!Enum.TryParse<ProjectionChangeKind>(response.ChangeKind, ignoreCase: true, out var changeKind))
        {
            _logger.LogWarning(
                "Ignoring unknown projection change kind '{ChangeKind}' for change {ChangeId}",
                response.ChangeKind,
                response.Id);
            return null;
        }

        return new ProjectionChangeEvent
        {
            EntityKey = string.IsNullOrWhiteSpace(response.EntityKey)
                ? null
                : response.EntityKey.Trim(),
            Id = response.Id,
            WorkspaceId = response.WorkspaceId,
            Kind = changeKind,
            OccurredAtUtc = DateTimeOffset.Parse(response.OccurredAtUtc)
        };
    }
}
