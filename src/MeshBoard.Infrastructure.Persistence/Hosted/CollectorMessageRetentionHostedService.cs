using MeshBoard.Application.Services;
using MeshBoard.Contracts.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MeshBoard.Infrastructure.Persistence.Hosted;

internal sealed class CollectorMessageRetentionHostedService : BackgroundService
{
    private static readonly TimeSpan PruneInterval = TimeSpan.FromHours(24);

    private readonly ILogger<CollectorMessageRetentionHostedService> _logger;
    private readonly PersistenceOptions _persistenceOptions;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public CollectorMessageRetentionHostedService(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<PersistenceOptions> persistenceOptions,
        ILogger<CollectorMessageRetentionHostedService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _persistenceOptions = persistenceOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_persistenceOptions.MessageRetentionDays <= 0)
        {
            _logger.LogInformation("Collector message retention is disabled.");
            return;
        }

        await PruneAsync(stoppingToken);

        using var timer = new PeriodicTimer(PruneInterval);

        while (!stoppingToken.IsCancellationRequested &&
               await timer.WaitForNextTickAsync(stoppingToken))
        {
            await PruneAsync(stoppingToken);
        }
    }

    private async Task PruneAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _serviceScopeFactory.CreateAsyncScope();
            var messageRetentionService = scope.ServiceProvider.GetRequiredService<IMessageRetentionService>();
            await messageRetentionService.PruneExpiredMessages(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Pruning collector messages failed.");
        }
    }
}
