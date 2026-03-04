using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Infrastructure.Persistence.Initialization;

internal sealed class PersistenceInitializationHostedService : IHostedService
{
    private readonly SqliteDatabaseInitializer _databaseInitializer;
    private readonly ILogger<PersistenceInitializationHostedService> _logger;

    public PersistenceInitializationHostedService(
        SqliteDatabaseInitializer databaseInitializer,
        ILogger<PersistenceInitializationHostedService> logger)
    {
        _databaseInitializer = databaseInitializer;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Attempting to run persistence initialization hosted service");
        await _databaseInitializer.InitializeAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
