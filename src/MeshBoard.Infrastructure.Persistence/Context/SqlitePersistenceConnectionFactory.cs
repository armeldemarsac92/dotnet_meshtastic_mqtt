using System.Data.Common;
using MeshBoard.Contracts.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace MeshBoard.Infrastructure.Persistence.Context;

internal sealed class SqlitePersistenceConnectionFactory : IPersistenceConnectionFactory
{
    private readonly PersistenceOptions _options;

    public SqlitePersistenceConnectionFactory(IOptions<PersistenceOptions> options)
    {
        _options = options.Value;
    }

    public DbConnection CreateConnection()
    {
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            throw new InvalidOperationException("The persistence connection string is not configured.");
        }

        return new SqliteConnection(_options.ConnectionString);
    }
}
