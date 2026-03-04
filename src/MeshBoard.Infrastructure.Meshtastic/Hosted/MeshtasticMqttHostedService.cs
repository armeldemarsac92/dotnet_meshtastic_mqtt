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
    private readonly BrokerOptions _brokerOptions;
    private readonly IMeshtasticEnvelopeReader _envelopeReader;
    private readonly ILogger<MeshtasticMqttHostedService> _logger;
    private readonly IMqttSession _mqttSession;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public MeshtasticMqttHostedService(
        IMqttSession mqttSession,
        IMeshtasticEnvelopeReader envelopeReader,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<BrokerOptions> brokerOptions,
        ILogger<MeshtasticMqttHostedService> logger)
    {
        _mqttSession = mqttSession;
        _envelopeReader = envelopeReader;
        _serviceScopeFactory = serviceScopeFactory;
        _brokerOptions = brokerOptions.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _mqttSession.MessageReceived += OnMessageReceived;

        _logger.LogInformation("Attempting to start the Meshtastic MQTT hosted service");

        try
        {
            await _mqttSession.ConnectAsync(cancellationToken);
            await _mqttSession.SubscribeAsync(_brokerOptions.DefaultTopicPattern, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "The Meshtastic MQTT hosted service could not connect and subscribe during startup");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _mqttSession.MessageReceived -= OnMessageReceived;
        await _mqttSession.DisconnectAsync(cancellationToken);
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
