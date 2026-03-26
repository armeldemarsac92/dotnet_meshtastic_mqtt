using Dapper;
using MeshBoard.Infrastructure.Persistence.Context;
using MeshBoard.Infrastructure.Persistence.Migrations.Postgres;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Infrastructure.Persistence.Initialization;

internal sealed class PostgresCollectorSchemaInitializer : IPersistenceInitializer
{
    private readonly IPersistenceConnectionFactory _connectionFactory;
    private readonly ILogger<PostgresCollectorSchemaInitializer> _logger;

    public PostgresCollectorSchemaInitializer(
        IPersistenceConnectionFactory connectionFactory,
        ILogger<PostgresCollectorSchemaInitializer> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting to initialize the PostgreSQL collector schema migrations");

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        foreach (var resourceName in CollectorMigrationScriptCatalog.GetOrderedResourceNames())
        {
            _logger.LogInformation("Applying PostgreSQL collector migration script {ResourceName}", resourceName);

            await connection.ExecuteAsync(
                new CommandDefinition(
                    CollectorMigrationScriptCatalog.ReadScript(resourceName),
                    cancellationToken: cancellationToken));
        }

        _logger.LogInformation("Initialized the PostgreSQL collector schema migrations successfully");
    }
}
