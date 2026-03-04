using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Application.Services;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Exceptions;
using MeshBoard.Contracts.Meshtastic;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MeshBoard.UnitTests;

public sealed class BrokerMonitorServiceTests
{
    [Fact]
    public async Task SubscribeToTopic_ShouldConnectAndSubscribe_WithTrimmedFilter()
    {
        var mqttSession = new FakeMqttSession(isConnected: false);
        var service = CreateService(mqttSession);

        await service.SubscribeToTopic("  msh/US/2/e/LongFast/#  ");

        Assert.Equal(1, mqttSession.ConnectCalls);
        Assert.Equal("msh/US/2/e/LongFast/#", mqttSession.LastSubscribedFilter);
    }

    [Fact]
    public async Task UnsubscribeFromTopic_ShouldCallSession_WithTrimmedFilter()
    {
        var mqttSession = new FakeMqttSession(isConnected: true);
        var service = CreateService(mqttSession);

        await service.UnsubscribeFromTopic("  msh/US/2/e/LongFast/#  ");

        Assert.Equal("msh/US/2/e/LongFast/#", mqttSession.LastUnsubscribedFilter);
    }

    [Fact]
    public async Task SubscribeToTopic_ShouldThrowBadRequest_WhenFilterIsMissing()
    {
        var service = CreateService(new FakeMqttSession(isConnected: true));

        await Assert.ThrowsAsync<BadRequestException>(() => service.SubscribeToTopic(" "));
    }

    private static BrokerMonitorService CreateService(IMqttSession mqttSession)
    {
        return new BrokerMonitorService(
            mqttSession,
            Options.Create(new BrokerOptions { Host = "mqtt.meshtastic.org", Port = 1883 }),
            NullLogger<BrokerMonitorService>.Instance);
    }

#pragma warning disable CS0067
    private sealed class FakeMqttSession : IMqttSession
    {
        public FakeMqttSession(bool isConnected)
        {
            IsConnected = isConnected;
        }

        public int ConnectCalls { get; private set; }

        public bool IsConnected { get; private set; }

        public string? LastStatusMessage => null;

        public string? LastSubscribedFilter { get; private set; }

        public string? LastUnsubscribedFilter { get; private set; }

        public IReadOnlyCollection<string> TopicFilters => [];

        public event Func<bool, Task>? ConnectionStateChanged;

        public event Func<MqttInboundMessage, Task>? MessageReceived;

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            ConnectCalls++;
            IsConnected = true;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            IsConnected = false;
            return Task.CompletedTask;
        }

        public Task PublishAsync(string topic, string payload, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SubscribeAsync(string topicFilter, CancellationToken cancellationToken = default)
        {
            LastSubscribedFilter = topicFilter;
            return Task.CompletedTask;
        }

        public Task UnsubscribeAsync(string topicFilter, CancellationToken cancellationToken = default)
        {
            LastUnsubscribedFilter = topicFilter;
            return Task.CompletedTask;
        }
    }
#pragma warning restore CS0067
}
