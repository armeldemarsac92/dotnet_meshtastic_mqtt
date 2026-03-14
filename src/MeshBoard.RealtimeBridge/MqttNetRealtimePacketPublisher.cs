using MeshBoard.Application.Abstractions.Realtime;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Realtime;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Protocol;

namespace MeshBoard.RealtimeBridge;

internal sealed class MqttNetRealtimePacketPublisher : IRealtimePacketPublisher, IAsyncDisposable
{
    private readonly IMqttClient _mqttClient;
    private readonly ILogger<MqttNetRealtimePacketPublisher> _logger;
    private readonly IRealtimePacketPublicationFactory _publicationFactory;
    private readonly RealtimeDownstreamBrokerOptions _options;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public MqttNetRealtimePacketPublisher(
        IRealtimePacketPublicationFactory publicationFactory,
        IOptions<RealtimeDownstreamBrokerOptions> options,
        ILogger<MqttNetRealtimePacketPublisher> logger)
    {
        _publicationFactory = publicationFactory;
        _options = options.Value;
        _logger = logger;

        var factory = new MqttClientFactory();
        _mqttClient = factory.CreateMqttClient();
        _mqttClient.ConnectedAsync += OnConnectedAsync;
        _mqttClient.DisconnectedAsync += OnDisconnectedAsync;
    }

    public async ValueTask DisposeAsync()
    {
        if (_mqttClient.IsConnected)
        {
            try
            {
                await _mqttClient.DisconnectAsync();
            }
            catch (Exception exception)
            {
                _logger.LogDebug(exception, "Ignoring downstream broker disconnect failure during bridge shutdown.");
            }
        }

        _mqttClient.Dispose();
        _semaphore.Dispose();
    }

    public async Task PublishAsync(RealtimePacketEnvelope envelope)
    {
        var publication = _publicationFactory.Create(envelope);

        await EnsureConnectedAsync();

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(publication.Topic)
            .WithContentType(publication.ContentType)
            .WithPayload(publication.Payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
            .Build();

        var result = await _mqttClient.PublishAsync(message);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(
                $"Publishing realtime packet to topic '{publication.Topic}' failed. Reason: {result.ReasonCode}; {result.ReasonString}");
        }

        _logger.LogDebug(
            "Published realtime packet for workspace {WorkspaceId} to downstream topic {Topic} ({PayloadBytes} bytes)",
            envelope.WorkspaceId,
            publication.Topic,
            publication.Payload.Length);
    }

    private Task OnConnectedAsync(MqttClientConnectedEventArgs eventArgs)
    {
        _logger.LogInformation(
            "Connected realtime bridge publisher to downstream broker {Host}:{Port}",
            _options.Host,
            _options.Port);
        return Task.CompletedTask;
    }

    private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs eventArgs)
    {
        _logger.LogWarning(
            eventArgs.Exception,
            "Disconnected realtime bridge publisher from downstream broker {Host}:{Port}. Reason: {Reason}",
            _options.Host,
            _options.Port,
            eventArgs.ReasonString);
        return Task.CompletedTask;
    }

    private async Task EnsureConnectedAsync()
    {
        if (_mqttClient.IsConnected)
        {
            return;
        }

        await _semaphore.WaitAsync();

        try
        {
            if (_mqttClient.IsConnected)
            {
                return;
            }

            ValidateOptions(_options);

            var builder = new MqttClientOptionsBuilder()
                .WithClientId(_options.ClientId.Trim())
                .WithTcpServer(_options.Host.Trim(), _options.Port)
                .WithCleanSession();

            if (!string.IsNullOrWhiteSpace(_options.Username))
            {
                builder.WithCredentials(_options.Username, _options.Password);
            }

            if (_options.UseTls)
            {
                builder.WithTlsOptions(tls => tls.UseTls());
            }

            var connectResult = await _mqttClient.ConnectAsync(builder.Build());
            if (!_mqttClient.IsConnected)
            {
                throw new InvalidOperationException(
                    $"Connecting the realtime bridge publisher to downstream broker '{_options.Host}:{_options.Port}' failed. Reason: {connectResult.ResultCode}; {connectResult.ReasonString}");
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static void ValidateOptions(RealtimeDownstreamBrokerOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Host))
        {
            throw new InvalidOperationException("RealtimeDownstreamBroker:Host is required.");
        }

        if (options.Port <= 0)
        {
            throw new InvalidOperationException("RealtimeDownstreamBroker:Port must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(options.ClientId))
        {
            throw new InvalidOperationException("RealtimeDownstreamBroker:ClientId is required.");
        }
    }
}
