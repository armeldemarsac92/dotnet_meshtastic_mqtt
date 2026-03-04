namespace MeshBoard.Infrastructure.Persistence.Context;

internal interface IDbContext
{
    Task<int> ExecuteAsync(string sql, object? parameters = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<T>> QueryAsync<T>(string sql, object? parameters = null, CancellationToken cancellationToken = default);

    Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? parameters = null, CancellationToken cancellationToken = default);
}
