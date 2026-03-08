using MeshBoard.Application.Abstractions.Persistence;
using Microsoft.Extensions.Hosting;

namespace MeshBoard.Web.State;

internal sealed class ProjectionChangeDispatchHostedService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RetentionWindow = TimeSpan.FromMinutes(30);
    private const int BatchSize = 256;

    private readonly ILogger<ProjectionChangeDispatchHostedService> _logger;
    private readonly ProjectionChangeNotifier _projectionChangeNotifier;
    private readonly IProjectionChangeRepository _projectionChangeRepository;
    private readonly TimeProvider _timeProvider;

    public ProjectionChangeDispatchHostedService(
        IProjectionChangeRepository projectionChangeRepository,
        ProjectionChangeNotifier projectionChangeNotifier,
        TimeProvider timeProvider,
        ILogger<ProjectionChangeDispatchHostedService> logger)
    {
        _projectionChangeRepository = projectionChangeRepository;
        _projectionChangeNotifier = projectionChangeNotifier;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var lastSeenId = await _projectionChangeRepository.GetLatestIdAsync(stoppingToken);
        var lastPrunedAtUtc = _timeProvider.GetUtcNow();

        using var timer = new PeriodicTimer(PollInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                lastSeenId = await DispatchPendingChangesAsync(lastSeenId, stoppingToken);

                if ((_timeProvider.GetUtcNow() - lastPrunedAtUtc) >= TimeSpan.FromMinutes(5))
                {
                    await _projectionChangeRepository.DeleteOlderThanAsync(
                        _timeProvider.GetUtcNow() - RetentionWindow,
                        stoppingToken);
                    lastPrunedAtUtc = _timeProvider.GetUtcNow();
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Failed to dispatch projection changes to connected circuits.");
            }
        }
    }

    private async Task<long> DispatchPendingChangesAsync(long lastSeenId, CancellationToken cancellationToken)
    {
        while (true)
        {
            var changes = await _projectionChangeRepository.GetChangesAfterAsync(lastSeenId, BatchSize, cancellationToken);

            if (changes.Count == 0)
            {
                return lastSeenId;
            }

            foreach (var change in changes)
            {
                await _projectionChangeNotifier.NotifyChangedAsync(change);
                lastSeenId = Math.Max(lastSeenId, change.Id);
            }

            if (changes.Count < BatchSize)
            {
                return lastSeenId;
            }
        }
    }
}
