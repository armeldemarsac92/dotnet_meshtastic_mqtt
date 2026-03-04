using System.Data;
using Dapper;
using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MeshBoard.Infrastructure.Persistence.Context;

internal sealed class DapperContext : IDbContext, IUnitOfWork, IAsyncDisposable
{
    private readonly ILogger<DapperContext> _logger;
    private readonly PersistenceOptions _options;
    private SqliteConnection? _connection;
    private SqliteTransaction? _transaction;

    public DapperContext(IOptions<PersistenceOptions> options, ILogger<DapperContext> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
        {
            throw new InvalidOperationException("A database transaction is already active.");
        }

        _logger.LogDebug("Attempting to begin a database transaction");

        _connection = CreateConnection();
        await _connection.OpenAsync(cancellationToken);
        _transaction = (SqliteTransaction)await _connection.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
        {
            return;
        }

        _logger.LogDebug("Attempting to commit the active database transaction");

        await _transaction.CommitAsync(cancellationToken);
        await DisposeTransactionAsync();
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
        {
            return;
        }

        _logger.LogWarning("Attempting to rollback the active database transaction");

        await _transaction.RollbackAsync(cancellationToken);
        await DisposeTransactionAsync();
    }

    public Task<int> ExecuteAsync(string sql, object? parameters = null, CancellationToken cancellationToken = default)
    {
        return ExecuteWithConnectionAsync(
            async (connection, transaction) =>
            {
                var command = new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken);
                return await connection.ExecuteAsync(command);
            },
            cancellationToken);
    }

    public Task<IReadOnlyCollection<T>> QueryAsync<T>(
        string sql,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteWithConnectionAsync(
            async (connection, transaction) =>
            {
                var command = new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken);
                var results = await connection.QueryAsync<T>(command);
                return (IReadOnlyCollection<T>)results.AsList();
            },
            cancellationToken);
    }

    public Task<T?> QueryFirstOrDefaultAsync<T>(
        string sql,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteWithConnectionAsync(
            async (connection, transaction) =>
            {
                var command = new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken);
                return await connection.QueryFirstOrDefaultAsync<T>(command);
            },
            cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeTransactionAsync();
    }

    private SqliteConnection CreateConnection()
    {
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            throw new InvalidOperationException("The persistence connection string is not configured.");
        }

        return new SqliteConnection(_options.ConnectionString);
    }

    private async Task<T> ExecuteWithConnectionAsync<T>(
        Func<SqliteConnection, SqliteTransaction?, Task<T>> action,
        CancellationToken cancellationToken)
    {
        if (_connection is not null && _transaction is not null)
        {
            return await action(_connection, _transaction);
        }

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        return await action(connection, null);
    }

    private async Task DisposeTransactionAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}
