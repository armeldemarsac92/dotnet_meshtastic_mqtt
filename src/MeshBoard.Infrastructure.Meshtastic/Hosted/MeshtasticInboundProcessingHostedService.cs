using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Application.Services;
using MeshBoard.Contracts.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MeshBoard.Infrastructure.Meshtastic.Hosted;

internal sealed class MeshtasticInboundProcessingHostedService : BackgroundService
{
    private readonly IMeshtasticEnvelopeReader _envelopeReader;
    private readonly MeshtasticInboundMessageQueue _inboundMessageQueue;
    private readonly ILogger<MeshtasticInboundProcessingHostedService> _logger;
    private readonly MeshtasticRuntimeOptions _runtimeOptions;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly TimeProvider _timeProvider;

    public MeshtasticInboundProcessingHostedService(
        MeshtasticInboundMessageQueue inboundMessageQueue,
        IMeshtasticEnvelopeReader envelopeReader,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<MeshtasticRuntimeOptions> runtimeOptions,
        TimeProvider timeProvider,
        ILogger<MeshtasticInboundProcessingHostedService> logger)
    {
        _inboundMessageQueue = inboundMessageQueue;
        _envelopeReader = envelopeReader;
        _serviceScopeFactory = serviceScopeFactory;
        _runtimeOptions = runtimeOptions.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workerCount = Math.Max(1, _runtimeOptions.InboundWorkerCount);

        _logger.LogInformation(
            "Starting Meshtastic inbound processing with {WorkerCount} worker(s)",
            workerCount);

        var workers = Enumerable.Range(0, workerCount)
            .Select(workerId => RunWorkerAsync(workerId, stoppingToken))
            .ToArray();

        await Task.WhenAll(workers);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _inboundMessageQueue.Complete();
        return base.StopAsync(cancellationToken);
    }

    private async Task RunWorkerAsync(int workerId, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var queueItem in _inboundMessageQueue.ReadAllAsync(cancellationToken))
            {
                await ProcessAsync(workerId, queueItem, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task ProcessAsync(
        int workerId,
        MeshtasticInboundQueueItem queueItem,
        CancellationToken cancellationToken)
    {
        var inboundMessage = queueItem.InboundMessage;
        var timeInQueue = _timeProvider.GetUtcNow() - queueItem.EnqueuedAtUtc;

        if (timeInQueue > TimeSpan.FromSeconds(5))
        {
            _logger.LogWarning(
                "Meshtastic inbound message for workspace {WorkspaceId} spent {QueueDelayMs} ms in queue before processing on worker {WorkerId}",
                inboundMessage.WorkspaceId,
                timeInQueue.TotalMilliseconds,
                workerId);
        }

        var envelope = await _envelopeReader.Read(
            inboundMessage.WorkspaceId,
            inboundMessage.Topic,
            inboundMessage.Payload,
            cancellationToken);

        if (envelope is null)
        {
            return;
        }

        envelope.ReceivedAtUtc = inboundMessage.ReceivedAtUtc;

        if (string.IsNullOrWhiteSpace(envelope.BrokerServer))
        {
            envelope.BrokerServer = inboundMessage.BrokerServer;
        }

        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var ingestionService = scope.ServiceProvider.GetRequiredService<IMeshtasticIngestionService>();
        await ingestionService.IngestEnvelope(envelope, cancellationToken);
    }
}
