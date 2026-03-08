using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Meshtastic;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MeshBoard.Infrastructure.Meshtastic.Hosted;

internal sealed class MeshtasticRuntimeMetricsHostedService : BackgroundService
{
    private readonly MeshtasticInboundMessageQueue _inboundMessageQueue;
    private readonly ILogger<MeshtasticRuntimeMetricsHostedService> _logger;
    private readonly IBrokerRuntimeRegistry _brokerRuntimeRegistry;
    private readonly MeshtasticRuntimeOptions _runtimeOptions;
    private readonly TimeProvider _timeProvider;

    public MeshtasticRuntimeMetricsHostedService(
        MeshtasticInboundMessageQueue inboundMessageQueue,
        IBrokerRuntimeRegistry brokerRuntimeRegistry,
        IOptions<MeshtasticRuntimeOptions> runtimeOptions,
        TimeProvider timeProvider,
        ILogger<MeshtasticRuntimeMetricsHostedService> logger)
    {
        _inboundMessageQueue = inboundMessageQueue;
        _brokerRuntimeRegistry = brokerRuntimeRegistry;
        _runtimeOptions = runtimeOptions.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var publishInterval = TimeSpan.FromMilliseconds(Math.Max(250, _runtimeOptions.MetricsPublishIntervalMilliseconds));

        _logger.LogInformation(
            "Starting Meshtastic runtime metrics publisher with interval {PublishIntervalMs} ms",
            publishInterval.TotalMilliseconds);

        using var timer = new PeriodicTimer(publishInterval);

        await PublishSnapshotAsync();

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await PublishSnapshotAsync();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            await PublishSnapshotAsync();
        }
    }

    private Task PublishSnapshotAsync()
    {
        foreach (var queueSnapshot in _inboundMessageQueue.GetSnapshots())
        {
            if (string.IsNullOrWhiteSpace(queueSnapshot.WorkspaceId))
            {
                continue;
            }

            _brokerRuntimeRegistry.UpdatePipelineSnapshot(
                queueSnapshot.WorkspaceId,
                new RuntimePipelineSnapshot
                {
                    InboundQueueCapacity = queueSnapshot.Capacity,
                    InboundWorkerCount = Math.Max(1, _runtimeOptions.InboundWorkerCount),
                    InboundQueueDepth = queueSnapshot.CurrentDepth,
                    InboundOldestMessageAgeMilliseconds = queueSnapshot.OldestMessageAgeMilliseconds,
                    InboundEnqueuedCount = queueSnapshot.EnqueuedCount,
                    InboundDequeuedCount = queueSnapshot.DequeuedCount,
                    InboundDroppedCount = queueSnapshot.DroppedCount,
                    UpdatedAtUtc = _timeProvider.GetUtcNow()
                });
        }

        return Task.CompletedTask;
    }
}
