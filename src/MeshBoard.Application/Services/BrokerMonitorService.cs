using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Exceptions;
using MeshBoard.Contracts.Meshtastic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MeshBoard.Application.Services;

public interface IBrokerMonitorService
{
    Task EnsureConnected(CancellationToken cancellationToken = default);

    BrokerStatus GetBrokerStatus();

    Task SubscribeToTopic(string topicFilter, CancellationToken cancellationToken = default);
}

public sealed class BrokerMonitorService : IBrokerMonitorService
{
    private readonly BrokerOptions _brokerOptions;
    private readonly ILogger<BrokerMonitorService> _logger;
    private readonly IMqttSession _mqttSession;

    public BrokerMonitorService(
        IMqttSession mqttSession,
        IOptions<BrokerOptions> brokerOptions,
        ILogger<BrokerMonitorService> logger)
    {
        _mqttSession = mqttSession;
        _brokerOptions = brokerOptions.Value;
        _logger = logger;
    }

    public async Task EnsureConnected(CancellationToken cancellationToken = default)
    {
        if (_mqttSession.IsConnected)
        {
            return;
        }

        _logger.LogInformation("Attempting to ensure the MQTT session is connected");

        await _mqttSession.ConnectAsync(cancellationToken);
    }

    public BrokerStatus GetBrokerStatus()
    {
        return new BrokerStatus
        {
            Host = _brokerOptions.Host,
            Port = _brokerOptions.Port,
            IsConnected = _mqttSession.IsConnected,
            LastStatusMessage = _mqttSession.LastStatusMessage,
            TopicFilters = _mqttSession.TopicFilters.ToList()
        };
    }

    public async Task SubscribeToTopic(string topicFilter, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(topicFilter))
        {
            throw new BadRequestException("A topic filter is required.");
        }

        _logger.LogInformation("Attempting to subscribe to topic filter: {TopicFilter}", topicFilter);

        await EnsureConnected(cancellationToken);
        await _mqttSession.SubscribeAsync(topicFilter.Trim(), cancellationToken);
    }
}
