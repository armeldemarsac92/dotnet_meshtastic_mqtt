using System.Collections.Concurrent;
using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Application.Services;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Meshtastic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Protocol;

namespace MeshBoard.Infrastructure.Meshtastic.Mqtt;

internal sealed class MqttSession : IMqttSession
{
    private readonly BrokerOptions _fallbackBrokerOptions;
    private readonly IMqttClient _mqttClient;
    private readonly ILogger<MqttSession> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly ConcurrentDictionary<string, byte> _topicFilters = new(StringComparer.Ordinal);
    private string _connectedBrokerServer = "unknown";
    private string _currentClientId = "meshboard";
    private string? _lastStatusMessage;

    public MqttSession(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<BrokerOptions> brokerOptions,
        ILogger<MqttSession> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _fallbackBrokerOptions = brokerOptions.Value;
        _currentClientId = CreateClientId(_fallbackBrokerOptions.ClientId);
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

            var settings = await ResolveConnectionSettingsAsync(cancellationToken);
            _currentClientId = CreateClientId(_fallbackBrokerOptions.ClientId);

            _logger.LogInformation(
                "Attempting to connect to MQTT broker: {Host}:{Port}",
                settings.Host,
                settings.Port);

            var builder = new MqttClientOptionsBuilder()
                .WithClientId(_currentClientId)
                .WithTcpServer(settings.Host, settings.Port)
                .WithCleanSession();

            if (!string.IsNullOrWhiteSpace(settings.Username))
            {
                builder.WithCredentials(settings.Username, settings.Password);
            }

            if (settings.UseTls)
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

            _connectedBrokerServer = settings.ServerAddress;
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

    private static string CreateClientId(string configuredClientId)
    {
        if (!string.IsNullOrWhiteSpace(configuredClientId) && configuredClientId != "meshboard-local")
        {
            return configuredClientId;
        }

        return $"meshboard-{Environment.MachineName.ToLowerInvariant()}-{Guid.NewGuid():N}";
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

    private async Task<ConnectionSettings> ResolveConnectionSettingsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _serviceScopeFactory.CreateAsyncScope();
            var profileService = scope.ServiceProvider.GetRequiredService<IBrokerServerProfileService>();
            var activeProfile = await profileService.GetActiveServerProfile(cancellationToken);

            return new ConnectionSettings
            {
                Host = activeProfile.Host,
                Port = activeProfile.Port,
                UseTls = activeProfile.UseTls,
                Username = activeProfile.Username,
                Password = activeProfile.Password
            };
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Falling back to static broker options because active profile resolution failed.");

            return new ConnectionSettings
            {
                Host = _fallbackBrokerOptions.Host,
                Port = _fallbackBrokerOptions.Port,
                UseTls = _fallbackBrokerOptions.UseTls,
                Username = _fallbackBrokerOptions.Username,
                Password = _fallbackBrokerOptions.Password
            };
        }
    }

    private sealed class ConnectionSettings
    {
        public required string Host { get; set; }

        public int Port { get; set; }

        public bool UseTls { get; set; }

        public string Username { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public string ServerAddress => $"{Host}:{Port}";
    }
}
