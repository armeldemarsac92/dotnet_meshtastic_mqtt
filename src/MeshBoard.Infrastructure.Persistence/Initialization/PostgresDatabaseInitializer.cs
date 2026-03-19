using Dapper;
using MeshBoard.Infrastructure.Persistence.Context;
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
        _logger.LogInformation("Attempting to validate PostgreSQL persistence connectivity");

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var result = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition("SELECT 1;", cancellationToken: cancellationToken));

        if (result != 1)
        {
            throw new InvalidOperationException("PostgreSQL connectivity validation returned an unexpected result.");
        }

        _logger.LogInformation("Validated PostgreSQL persistence connectivity successfully");
    }
}
