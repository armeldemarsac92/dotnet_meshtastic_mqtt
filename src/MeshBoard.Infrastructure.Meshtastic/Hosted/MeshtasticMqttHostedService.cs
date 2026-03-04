using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Application.Services;
using MeshBoard.Contracts.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MeshBoard.Infrastructure.Meshtastic.Hosted;

internal sealed class MeshtasticMqttHostedService : IHostedService
{
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly BrokerOptions _brokerOptions;
    private readonly IMeshtasticEnvelopeReader _envelopeReader;
    private readonly ILogger<MeshtasticMqttHostedService> _logger;
    private readonly IMqttSession _mqttSession;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly CancellationTokenSource _stoppingTokenSource = new();
    private Task? _startupTask;

    public MeshtasticMqttHostedService(
        IMqttSession mqttSession,
        IMeshtasticEnvelopeReader envelopeReader,
        IServiceScopeFactory serviceScopeFactory,
        IHostApplicationLifetime applicationLifetime,
        IOptions<BrokerOptions> brokerOptions,
        ILogger<MeshtasticMqttHostedService> logger)
    {
        _mqttSession = mqttSession;
        _envelopeReader = envelopeReader;
        _serviceScopeFactory = serviceScopeFactory;
        _applicationLifetime = applicationLifetime;
        _brokerOptions = brokerOptions.Value;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _mqttSession.MessageReceived += OnMessageReceived;

        _applicationLifetime.ApplicationStarted.Register(
            () => _startupTask = Task.Run(() => ConnectAndSubscribeAsync(_stoppingTokenSource.Token), CancellationToken.None));

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _stoppingTokenSource.Cancel();
        _mqttSession.MessageReceived -= OnMessageReceived;

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

        await _mqttSession.DisconnectAsync(cancellationToken);
        _stoppingTokenSource.Dispose();
    }

    private async Task ConnectAndSubscribeAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Attempting to start the Meshtastic MQTT hosted service");

        try
        {
            await _mqttSession.ConnectAsync(cancellationToken);
            await _mqttSession.SubscribeAsync(_brokerOptions.DefaultTopicPattern, cancellationToken);
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
        var envelope = await _envelopeReader.Read(inboundMessage.Topic, inboundMessage.Payload);

        if (envelope is null)
        {
            return;
        }

        if (envelope.ReceivedAtUtc == default)
        {
            envelope.ReceivedAtUtc = inboundMessage.ReceivedAtUtc;
        }

        using var scope = _serviceScopeFactory.CreateScope();
        var ingestionService = scope.ServiceProvider.GetRequiredService<IMeshtasticIngestionService>();

        await ingestionService.IngestEnvelope(envelope);
    }
}
