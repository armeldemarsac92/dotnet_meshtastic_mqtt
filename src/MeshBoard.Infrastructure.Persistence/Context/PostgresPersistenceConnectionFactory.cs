using System.Data.Common;
using MeshBoard.Contracts.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;

namespace MeshBoard.Infrastructure.Persistence.Context;

internal sealed class PostgresPersistenceConnectionFactory : IPersistenceConnectionFactory
{
    private readonly PersistenceOptions _options;

    public PostgresPersistenceConnectionFactory(IOptions<PersistenceOptions> options)
    {
        _options = options.Value;
    }

    public DbConnection CreateConnection()
    {
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            throw new InvalidOperationException("The persistence connection string is not configured.");
        }

        return new NpgsqlConnection(_options.ConnectionString);
    }
}
