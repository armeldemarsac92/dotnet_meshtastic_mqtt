using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Application.Services;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Meshtastic;
using Microsoft.Extensions.Options;

namespace MeshBoard.UnitTests;

public sealed class SendCapabilityServiceTests
{
    [Fact]
    public void GetStatus_ShouldBeBlocked_WhenSendDisabled()
    {
        var service = CreateService(
            new BrokerOptions
            {
                Host = "mqtt.meshtastic.org",
                Port = 1883,
                EnableSend = false,
                DownlinkTopic = "msh/US/2/json/mqtt/"
            },
            isConnected: true);

        var status = service.GetStatus();

        Assert.False(status.IsEnabled);
        Assert.Contains(status.BlockingReasons, reason => reason.Contains("disabled by configuration", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetStatus_ShouldBeBlocked_WhenMqttDisconnected()
    {
        var service = CreateService(
            new BrokerOptions
            {
                Host = "mqtt.meshtastic.org",
                Port = 1883,
                EnableSend = true,
                DownlinkTopic = "msh/US/2/json/mqtt/"
            },
            isConnected: false);

        var status = service.GetStatus();

        Assert.False(status.IsEnabled);
        Assert.Contains(status.BlockingReasons, reason => reason.Contains("not connected", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetStatus_ShouldBeEnabled_WhenPreconditionsAreMet()
    {
        var service = CreateService(
            new BrokerOptions
            {
                Host = "mqtt.meshtastic.org",
                Port = 1883,
                EnableSend = true,
                DownlinkTopic = "msh/US/2/json/mqtt/"
            },
            isConnected: true);

        var status = service.GetStatus();

        Assert.True(status.IsEnabled);
        Assert.Empty(status.BlockingReasons);
        Assert.Equal("mqtt.meshtastic.org", status.Host);
        Assert.Equal(1883, status.Port);
    }

    private static SendCapabilityService CreateService(BrokerOptions options, bool isConnected)
    {
        return new SendCapabilityService(Options.Create(options), new FakeMqttSession(isConnected));
    }

#pragma warning disable CS0067
    private sealed class FakeMqttSession : IMqttSession
    {
        public FakeMqttSession(bool isConnected)
        {
            IsConnected = isConnected;
        }

        public bool IsConnected { get; }

        public string? LastStatusMessage => null;

        public IReadOnlyCollection<string> TopicFilters => [];

        public event Func<bool, Task>? ConnectionStateChanged;

        public event Func<MqttInboundMessage, Task>? MessageReceived;

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task PublishAsync(string topic, string payload, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SubscribeAsync(string topicFilter, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
#pragma warning restore CS0067
}
