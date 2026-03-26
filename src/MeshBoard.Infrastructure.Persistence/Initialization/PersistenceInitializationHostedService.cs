using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Infrastructure.Persistence.Initialization;

internal sealed class PersistenceInitializationHostedService : IHostedService
{
    private readonly IReadOnlyCollection<IPersistenceInitializer> _databaseInitializers;
    private readonly ILogger<PersistenceInitializationHostedService> _logger;

    public PersistenceInitializationHostedService(
        IEnumerable<IPersistenceInitializer> databaseInitializers,
        ILogger<PersistenceInitializationHostedService> logger)
    {
        _databaseInitializers = databaseInitializers.ToArray();
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Attempting to run persistence initialization hosted service");

        foreach (var databaseInitializer in _databaseInitializers)
        {
            await databaseInitializer.InitializeAsync(cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
