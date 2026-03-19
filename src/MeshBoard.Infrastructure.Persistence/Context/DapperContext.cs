using System.Data.Common;
using Dapper;
using MeshBoard.Application.Abstractions.Persistence;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Infrastructure.Persistence.Context;

internal sealed class DapperContext : IDbContext, IUnitOfWork, IAsyncDisposable
{
    private readonly IPersistenceConnectionFactory _connectionFactory;
    private readonly ILogger<DapperContext> _logger;
    private DbConnection? _connection;
    private DbTransaction? _transaction;

    public DapperContext(IPersistenceConnectionFactory connectionFactory, ILogger<DapperContext> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
        {
            throw new InvalidOperationException("A database transaction is already active.");
        }

        _logger.LogDebug("Attempting to begin a database transaction");

        _connection = _connectionFactory.CreateConnection();
        await _connection.OpenAsync(cancellationToken);
        _transaction = await _connection.BeginTransactionAsync(cancellationToken);
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

        _logger.LogDebug("Attempting to rollback the active database transaction");

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

    private async Task<T> ExecuteWithConnectionAsync<T>(
        Func<DbConnection, DbTransaction?, Task<T>> action,
        CancellationToken cancellationToken)
    {
        if (_connection is not null && _transaction is not null)
        {
            return await action(_connection, _transaction);
        }

        await using var connection = _connectionFactory.CreateConnection();
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
