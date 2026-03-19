using Dapper;
using MeshBoard.Infrastructure.Persistence.Context;
using MeshBoard.Infrastructure.Persistence.SQL;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Infrastructure.Persistence.Initialization;

internal sealed class PostgresDatabaseInitializer : IPersistenceInitializer
{
    private readonly IPersistenceConnectionFactory _connectionFactory;
    private readonly ILogger<PostgresDatabaseInitializer> _logger;

    public PostgresDatabaseInitializer(
        IPersistenceConnectionFactory connectionFactory,
        ILogger<PostgresDatabaseInitializer> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting to initialize the PostgreSQL preference schema");

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(
            new CommandDefinition(
                PostgresPreferenceSchemaQueries.CreateSchema,
                cancellationToken: cancellationToken));

        _logger.LogInformation("Initialized the PostgreSQL preference schema successfully");
    }
}
