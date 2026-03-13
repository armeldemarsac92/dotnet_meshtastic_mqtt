using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Contracts.Meshtastic;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Infrastructure.Meshtastic.Hosted;

internal sealed class MqttInboundDispatchHostedService : IHostedService
{
    private readonly IWorkspaceBrokerSessionManager _brokerSessionManager;
    private readonly ILogger<MqttInboundDispatchHostedService> _logger;
    private readonly IReadOnlyCollection<IMqttInboundMessageSink> _messageSinks;

    public MqttInboundDispatchHostedService(
        IEnumerable<IMqttInboundMessageSink> messageSinks,
        IWorkspaceBrokerSessionManager brokerSessionManager,
        ILogger<MqttInboundDispatchHostedService> logger)
    {
        _messageSinks = messageSinks.ToArray();
        _brokerSessionManager = brokerSessionManager;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _brokerSessionManager.MessageReceived += OnMessageReceivedAsync;

        _logger.LogInformation(
            "Starting MQTT inbound dispatcher with {SinkCount} sink(s)",
            _messageSinks.Count);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _brokerSessionManager.MessageReceived -= OnMessageReceivedAsync;
        return Task.CompletedTask;
    }

    private async Task OnMessageReceivedAsync(MqttInboundMessage inboundMessage)
    {
        foreach (var sink in _messageSinks)
        {
            try
            {
                await sink.HandleAsync(inboundMessage);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "MQTT inbound sink {SinkType} failed for workspace {WorkspaceId} topic {Topic}",
                    sink.GetType().Name,
                    inboundMessage.WorkspaceId,
                    inboundMessage.Topic);
            }
        }
    }
}
