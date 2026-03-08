using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Meshtastic;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MeshBoard.Infrastructure.Meshtastic.Hosted;

internal sealed class BrokerRuntimeCommandProcessorHostedService : BackgroundService
{
    private readonly IBrokerRuntimeCommandExecutor _brokerRuntimeCommandExecutor;
    private readonly IBrokerRuntimeCommandRepository _brokerRuntimeCommandRepository;
    private readonly ILogger<BrokerRuntimeCommandProcessorHostedService> _logger;
    private readonly MeshtasticRuntimeOptions _runtimeOptions;
    private readonly TimeProvider _timeProvider;
    private readonly string _processorId = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    public BrokerRuntimeCommandProcessorHostedService(
        IBrokerRuntimeCommandRepository brokerRuntimeCommandRepository,
        IBrokerRuntimeCommandExecutor brokerRuntimeCommandExecutor,
        IOptions<MeshtasticRuntimeOptions> runtimeOptions,
        TimeProvider timeProvider,
        ILogger<BrokerRuntimeCommandProcessorHostedService> logger)
    {
        _brokerRuntimeCommandRepository = brokerRuntimeCommandRepository;
        _brokerRuntimeCommandExecutor = brokerRuntimeCommandExecutor;
        _runtimeOptions = runtimeOptions.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollDelay = TimeSpan.FromMilliseconds(Math.Max(50, _runtimeOptions.CommandProcessorPollIntervalMilliseconds));
        var leaseDuration = TimeSpan.FromSeconds(Math.Max(5, _runtimeOptions.CommandLeaseDurationSeconds));
        var batchSize = Math.Max(1, _runtimeOptions.CommandProcessorBatchSize);

        _logger.LogInformation(
            "Starting broker runtime command processor {ProcessorId} with batch size {BatchSize}",
            _processorId,
            batchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            var commands = await _brokerRuntimeCommandRepository.LeasePendingAsync(
                _processorId,
                batchSize,
                leaseDuration,
                stoppingToken);

            if (commands.Count == 0)
            {
                await Task.Delay(pollDelay, stoppingToken);
                continue;
            }

            foreach (var command in commands)
            {
                await ProcessCommandAsync(command, stoppingToken);
            }
        }
    }

    private async Task ProcessCommandAsync(BrokerRuntimeCommand command, CancellationToken cancellationToken)
    {
        try
        {
            await ExecuteCommandAsync(command, cancellationToken);
            await _brokerRuntimeCommandRepository.MarkCompletedAsync(command.Id, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var maxAttempts = Math.Max(1, _runtimeOptions.CommandMaxAttempts);

            if (command.AttemptCount >= maxAttempts)
            {
                _logger.LogWarning(
                    exception,
                    "Broker runtime command {CommandId} for workspace {WorkspaceId} failed permanently after {AttemptCount} attempts",
                    command.Id,
                    command.WorkspaceId,
                    command.AttemptCount);

                await _brokerRuntimeCommandRepository.MarkFailedAsync(
                    command.Id,
                    exception.Message,
                    cancellationToken);

                return;
            }

            var retryDelay = TimeSpan.FromMilliseconds(
                Math.Max(250, _runtimeOptions.CommandRetryDelayMilliseconds) * Math.Max(1, command.AttemptCount));
            var nextAvailableAtUtc = _timeProvider.GetUtcNow().Add(retryDelay);

            _logger.LogWarning(
                exception,
                "Broker runtime command {CommandId} for workspace {WorkspaceId} failed on attempt {AttemptCount}. Retrying at {RetryAtUtc}",
                command.Id,
                command.WorkspaceId,
                command.AttemptCount,
                nextAvailableAtUtc);

            await _brokerRuntimeCommandRepository.MarkPendingAsync(
                command.Id,
                nextAvailableAtUtc,
                exception.Message,
                cancellationToken);
        }
    }

    private Task ExecuteCommandAsync(BrokerRuntimeCommand command, CancellationToken cancellationToken)
    {
        return command.CommandType switch
        {
            BrokerRuntimeCommandType.EnsureConnected =>
                _brokerRuntimeCommandExecutor.EnsureConnectedAsync(command.WorkspaceId, cancellationToken),
            BrokerRuntimeCommandType.ReconcileActiveProfile =>
                _brokerRuntimeCommandExecutor.ReconcileActiveProfileAsync(command.WorkspaceId, cancellationToken),
            BrokerRuntimeCommandType.ResetAndReconnectActiveProfile =>
                _brokerRuntimeCommandExecutor.ResetAndReconnectActiveProfileAsync(command.WorkspaceId, cancellationToken),
            BrokerRuntimeCommandType.Publish =>
                _brokerRuntimeCommandExecutor.PublishAsync(
                    command.WorkspaceId,
                    command.Topic ?? throw new InvalidOperationException("Publish commands require a topic."),
                    command.Payload ?? throw new InvalidOperationException("Publish commands require a payload."),
                    cancellationToken),
            BrokerRuntimeCommandType.SubscribeEphemeral =>
                _brokerRuntimeCommandExecutor.SubscribeEphemeralAsync(
                    command.WorkspaceId,
                    command.TopicFilter ?? throw new InvalidOperationException("Subscribe commands require a topic filter."),
                    cancellationToken),
            BrokerRuntimeCommandType.UnsubscribeEphemeral =>
                _brokerRuntimeCommandExecutor.UnsubscribeEphemeralAsync(
                    command.WorkspaceId,
                    command.TopicFilter ?? throw new InvalidOperationException("Unsubscribe commands require a topic filter."),
                    cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported broker runtime command type '{command.CommandType}'.")
        };
    }
}
