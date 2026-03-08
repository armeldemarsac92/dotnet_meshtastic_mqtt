using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Application.Services;
using MeshBoard.Infrastructure.Meshtastic.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Infrastructure.Meshtastic.Hosted;

internal sealed class MeshtasticMqttHostedService : IHostedService
{
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly IBrokerRuntimeBootstrapService _brokerRuntimeBootstrapService;
    private readonly IWorkspaceBrokerSessionManager _brokerSessionManager;
    private readonly IMeshtasticEnvelopeReader _envelopeReader;
    private readonly ILogger<MeshtasticMqttHostedService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly CancellationTokenSource _stoppingTokenSource = new();
    private Task? _startupTask;

    public MeshtasticMqttHostedService(
        IBrokerRuntimeBootstrapService brokerRuntimeBootstrapService,
        IWorkspaceBrokerSessionManager brokerSessionManager,
        IMeshtasticEnvelopeReader envelopeReader,
        IServiceScopeFactory serviceScopeFactory,
        IHostApplicationLifetime applicationLifetime,
        ILogger<MeshtasticMqttHostedService> logger)
    {
        _brokerRuntimeBootstrapService = brokerRuntimeBootstrapService;
        _brokerSessionManager = brokerSessionManager;
        _envelopeReader = envelopeReader;
        _serviceScopeFactory = serviceScopeFactory;
        _applicationLifetime = applicationLifetime;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _brokerSessionManager.MessageReceived += OnMessageReceived;

        _applicationLifetime.ApplicationStarted.Register(
            () => _startupTask = Task.Run(() => ConnectAndSubscribeAsync(_stoppingTokenSource.Token), CancellationToken.None));

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _stoppingTokenSource.Cancel();
        _brokerSessionManager.MessageReceived -= OnMessageReceived;

        if (_startupTask is not null)
        {
            try
            {
                await _startupTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        await _brokerSessionManager.DisconnectAllAsync(cancellationToken);
        _stoppingTokenSource.Dispose();
    }

    private async Task ConnectAndSubscribeAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Attempting to start the Meshtastic MQTT hosted service");

        try
        {
            await _brokerRuntimeBootstrapService.InitializeActiveWorkspacesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "The Meshtastic MQTT hosted service could not connect and subscribe during startup");
        }
    }

    private async Task OnMessageReceived(MeshBoard.Contracts.Meshtastic.MqttInboundMessage inboundMessage)
    {
        var envelope = await _envelopeReader.Read(
            inboundMessage.WorkspaceId,
            inboundMessage.Topic,
            inboundMessage.Payload);

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

        await ingestionService.IngestEnvelope(envelope);
    }
}
