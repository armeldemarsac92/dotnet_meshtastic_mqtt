using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Meshtastic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Protocol;

namespace MeshBoard.Infrastructure.Meshtastic.Mqtt;

internal sealed class MqttSession : IMqttSession
{
    private readonly BrokerOptions _fallbackBrokerOptions;
    private readonly MqttSessionConnectionSettings _connectionSettings;
    private readonly IMqttClient _mqttClient;
    private readonly ILogger<MqttSession> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly ConcurrentDictionary<string, byte> _topicFilters = new(StringComparer.Ordinal);
    private string _connectedBrokerServer = "unknown";
    private string _currentClientId = "meshboard";
    private string? _lastStatusMessage;

    public MqttSession(
        MqttSessionConnectionSettings connectionSettings,
        IOptions<BrokerOptions> brokerOptions,
        ILogger<MqttSession> logger)
    {
        _connectionSettings = connectionSettings;
        _fallbackBrokerOptions = brokerOptions.Value;
        _currentClientId = CreateClientId(_fallbackBrokerOptions.ClientId, _connectionSettings);
        _logger = logger;

        var factory = new MqttClientFactory();
        _mqttClient = factory.CreateMqttClient();
        _mqttClient.ApplicationMessageReceivedAsync += OnApplicationMessageReceivedAsync;
        _mqttClient.ConnectedAsync += OnConnectedAsync;
        _mqttClient.DisconnectedAsync += OnDisconnectedAsync;
    }

    public bool IsConnected => _mqttClient.IsConnected;

    public string? LastStatusMessage => _lastStatusMessage;

    public IReadOnlyCollection<string> TopicFilters => _topicFilters.Keys.OrderBy(x => x).ToList();

    public event Func<bool, Task>? ConnectionStateChanged;

    public event Func<MqttInboundMessage, Task>? MessageReceived;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            if (_mqttClient.IsConnected)
            {
                return;
            }

            _currentClientId = CreateClientId(_fallbackBrokerOptions.ClientId, _connectionSettings);

            _logger.LogInformation(
                "Attempting to connect to MQTT broker: {Host}:{Port}",
                _connectionSettings.Host,
                _connectionSettings.Port);

            var builder = new MqttClientOptionsBuilder()
                .WithClientId(_currentClientId)
                .WithTcpServer(_connectionSettings.Host, _connectionSettings.Port)
                .WithCleanSession();

            if (!string.IsNullOrWhiteSpace(_connectionSettings.Username))
            {
                builder.WithCredentials(_connectionSettings.Username, _connectionSettings.Password);
            }

            if (_connectionSettings.UseTls)
            {
                builder.WithTlsOptions(_ => _.UseTls());
            }

            var connectResult = await _mqttClient.ConnectAsync(builder.Build(), cancellationToken);

            if (!_mqttClient.IsConnected)
            {
                _lastStatusMessage = CreateConnectFailureMessage(connectResult.ResultCode.ToString(), connectResult.ReasonString);
                _logger.LogWarning(
                    "The MQTT broker connection attempt completed without an active connection. ResultCode: {ResultCode}; Reason: {ReasonString}",
                    connectResult.ResultCode,
                    connectResult.ReasonString);
                return;
            }

            _connectedBrokerServer = _connectionSettings.ServerAddress;
            _lastStatusMessage = $"Connected to {_connectedBrokerServer} as {_currentClientId}.";

            foreach (var topicFilter in _topicFilters.Keys)
            {
                await SubscribeCoreAsync(topicFilter, cancellationToken);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            if (!_mqttClient.IsConnected)
            {
                return;
            }

            _logger.LogInformation("Attempting to disconnect from the MQTT broker");

            await _mqttClient.DisconnectAsync(cancellationToken: cancellationToken);
            _lastStatusMessage = "Disconnected from the MQTT broker.";
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task SubscribeAsync(string topicFilter, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(topicFilter))
        {
            throw new InvalidOperationException("A topic filter is required.");
        }

        var normalizedTopicFilter = topicFilter.Trim();
        _topicFilters.TryAdd(normalizedTopicFilter, 0);

        if (!_mqttClient.IsConnected)
        {
            await ConnectAsync(cancellationToken);
        }

        if (!_mqttClient.IsConnected)
        {
            throw new InvalidOperationException("The MQTT client is not connected.");
        }

        await SubscribeCoreAsync(normalizedTopicFilter, cancellationToken);
    }

    public async Task UnsubscribeAsync(string topicFilter, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(topicFilter))
        {
            throw new InvalidOperationException("A topic filter is required.");
        }

        var normalizedTopicFilter = topicFilter.Trim();
        _topicFilters.TryRemove(normalizedTopicFilter, out _);

        if (!_mqttClient.IsConnected)
        {
            _lastStatusMessage = $"Removed topic filter {normalizedTopicFilter}.";
            return;
        }

        _logger.LogInformation("Attempting to unsubscribe from MQTT topic filter: {TopicFilter}", normalizedTopicFilter);

        var options = new MqttClientUnsubscribeOptionsBuilder()
            .WithTopicFilter(normalizedTopicFilter)
            .Build();

        await _mqttClient.UnsubscribeAsync(options, cancellationToken);
        _lastStatusMessage = $"Unsubscribed from {normalizedTopicFilter}.";
    }

    public async Task PublishAsync(string topic, string payload, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            throw new InvalidOperationException("A publish topic is required.");
        }

        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new InvalidOperationException("A publish payload is required.");
        }

        if (!_mqttClient.IsConnected)
        {
            await ConnectAsync(cancellationToken);
        }

        if (!_mqttClient.IsConnected)
        {
            throw new InvalidOperationException("The MQTT client is not connected.");
        }

        _logger.LogInformation("Attempting to publish MQTT message to topic: {Topic}", topic);

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
            .Build();

        var result = await _mqttClient.PublishAsync(message, cancellationToken);

        if (!result.IsSuccess)
        {
            _lastStatusMessage = CreatePublishFailureMessage(result.ReasonCode.ToString(), result.ReasonString);
            throw new InvalidOperationException(_lastStatusMessage);
        }

        _lastStatusMessage = $"Published message to {topic}.";
    }

    private Task OnApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs eventArgs)
    {
        var handler = MessageReceived;

        if (handler is null)
        {
            return Task.CompletedTask;
        }

        var payload = eventArgs.ApplicationMessage.Payload;
        var payloadBytes = new byte[payload.Length];
        var offset = 0;

        foreach (var segment in payload)
        {
            segment.Span.CopyTo(payloadBytes.AsSpan(offset));
            offset += segment.Length;
        }

        return handler.Invoke(new MqttInboundMessage
        {
            WorkspaceId = _connectionSettings.WorkspaceId,
            BrokerServer = _connectedBrokerServer,
            Topic = eventArgs.ApplicationMessage.Topic,
            Payload = payloadBytes,
            ReceivedAtUtc = DateTimeOffset.UtcNow
        });
    }

    private Task OnConnectedAsync(MqttClientConnectedEventArgs eventArgs)
    {
        _logger.LogInformation("Connected to the MQTT broker successfully");
        _lastStatusMessage = $"Connected to {_connectedBrokerServer} as {_currentClientId}.";

        var handler = ConnectionStateChanged;

        return handler?.Invoke(true) ?? Task.CompletedTask;
    }

    private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs eventArgs)
    {
        _lastStatusMessage = CreateDisconnectMessage(eventArgs);
        _logger.LogWarning(
            "Disconnected from the MQTT broker. Reason: {Reason}; ResultCode: {ResultCode}; Reason: {ReasonString}",
            eventArgs.Reason,
            eventArgs.ConnectResult?.ResultCode,
            eventArgs.ConnectResult?.ReasonString);

        var handler = ConnectionStateChanged;

        return handler?.Invoke(false) ?? Task.CompletedTask;
    }

    private async Task SubscribeCoreAsync(string topicFilter, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Attempting to subscribe to MQTT topic filter: {TopicFilter}", topicFilter);

        var options = new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(
                filter =>
                {
                    filter.WithTopic(topicFilter);
                    filter.WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce);
                })
            .Build();

        await _mqttClient.SubscribeAsync(options, cancellationToken);
    }

    internal static string CreateClientId(
        string configuredClientId,
        MqttSessionConnectionSettings connectionSettings)
    {
        ArgumentNullException.ThrowIfNull(connectionSettings);

        var clientIdBase = NormalizeClientIdBase(configuredClientId);
        var identity = $"{connectionSettings.WorkspaceId}|{connectionSettings.BrokerServerProfileId:N}|{connectionSettings.Host}:{connectionSettings.Port}";

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        var suffix = Convert.ToHexString(hashBytes[..8]).ToLowerInvariant();
        var maxBaseLength = Math.Max(1, 64 - suffix.Length - 1);

        if (clientIdBase.Length > maxBaseLength)
        {
            clientIdBase = clientIdBase[..maxBaseLength];
        }

        return $"{clientIdBase}-{suffix}";
    }

    private static string NormalizeClientIdBase(string configuredClientId)
    {
        var rawValue = string.IsNullOrWhiteSpace(configuredClientId)
            ? "meshboard"
            : configuredClientId.Trim();

        var normalizedCharacters = rawValue
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray();
        var normalizedValue = new string(normalizedCharacters).Trim('-');

        return string.IsNullOrWhiteSpace(normalizedValue)
            ? "meshboard"
            : normalizedValue;
    }

    private static string CreateConnectFailureMessage(string resultCode, string? reasonString)
    {
        return string.IsNullOrWhiteSpace(reasonString)
            ? $"Connection failed: {resultCode}."
            : $"Connection failed: {resultCode} ({reasonString}).";
    }

    private static string CreateDisconnectMessage(MqttClientDisconnectedEventArgs eventArgs)
    {
        if (eventArgs.ConnectResult is null)
        {
            return $"Disconnected: {eventArgs.Reason}.";
        }

        return CreateConnectFailureMessage(
            eventArgs.ConnectResult.ResultCode.ToString(),
            eventArgs.ConnectResult.ReasonString);
    }

    private static string CreatePublishFailureMessage(string reasonCode, string? reasonString)
    {
        return string.IsNullOrWhiteSpace(reasonString)
            ? $"Publish failed: {reasonCode}."
            : $"Publish failed: {reasonCode} ({reasonString}).";
    }

}
