using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Application.Abstractions.Workspaces;
using MeshBoard.Application.Services;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Exceptions;
using MeshBoard.Contracts.Messages;
using MeshBoard.Contracts.Meshtastic;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MeshBoard.UnitTests;

public sealed class MessageComposerServiceTests
{
    [Fact]
    public async Task SendTextMessage_ShouldPublishPublicMessage_WhenCapabilityIsEnabled()
    {
        var brokerRuntimeCommandService = new FakeBrokerRuntimeCommandService();
        var service = CreateService(
            brokerRuntimeCommandService,
            new FakeSendCapabilityService(
                new SendCapabilityStatus
                {
                    IsEnabled = true,
                    IsBrokerConnected = true,
                    DownlinkTopic = "msh/US/2/json/mqtt/"
                }));

        var result = await service.SendTextMessage(
            new ComposeTextMessageRequest
            {
                Text = "hello mesh"
            });

        Assert.Equal("msh/US/2/json/mqtt/", brokerRuntimeCommandService.LastPublishedTopic);
        Assert.Equal("""{"type":"sendtext","payload":"hello mesh"}""", brokerRuntimeCommandService.LastPublishedPayload);
        Assert.False(result.IsPrivate);
    }

    [Fact]
    public async Task SendTextMessage_ShouldPublishPrivateMessage_WhenToNodeIdProvided()
    {
        var brokerRuntimeCommandService = new FakeBrokerRuntimeCommandService();
        var service = CreateService(
            brokerRuntimeCommandService,
            new FakeSendCapabilityService(
                new SendCapabilityStatus
                {
                    IsEnabled = true,
                    IsBrokerConnected = true,
                    DownlinkTopic = "msh/US/2/json/mqtt/"
                }));

        var result = await service.SendTextMessage(
            new ComposeTextMessageRequest
            {
                Text = "private ping",
                ToNodeId = "!ABCDEF12"
            });

        Assert.Equal("msh/US/2/json/mqtt/", brokerRuntimeCommandService.LastPublishedTopic);
        Assert.Equal("""{"type":"sendtext","payload":"private ping","to":"!abcdef12"}""", brokerRuntimeCommandService.LastPublishedPayload);
        Assert.True(result.IsPrivate);
        Assert.Equal("!abcdef12", result.ToNodeId);
    }

    [Fact]
    public async Task SendTextMessage_ShouldThrowBadRequest_WhenCapabilityIsBlocked()
    {
        var service = CreateService(
            new FakeBrokerRuntimeCommandService(),
            new FakeSendCapabilityService(
                new SendCapabilityStatus
                {
                    IsEnabled = false,
                    BlockingReasons = ["The MQTT session is not connected."]
                }));

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => service.SendTextMessage(new ComposeTextMessageRequest { Text = "hello" }));

        Assert.Contains("blocked", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendTextMessage_ShouldThrowBadRequest_WhenNodeIdIsInvalid()
    {
        var service = CreateService(
            new FakeBrokerRuntimeCommandService(),
            new FakeSendCapabilityService(
                new SendCapabilityStatus
                {
                    IsEnabled = true,
                    IsBrokerConnected = true,
                    DownlinkTopic = "msh/US/2/json/mqtt/"
                }));

        await Assert.ThrowsAsync<BadRequestException>(
            () => service.SendTextMessage(
                new ComposeTextMessageRequest
                {
                    Text = "hello",
                    ToNodeId = "node-123"
                }));
    }

    private static MessageComposerService CreateService(
        IBrokerRuntimeCommandService brokerRuntimeCommandService,
        ISendCapabilityService sendCapabilityService)
    {
        return new MessageComposerService(
            brokerRuntimeCommandService,
            sendCapabilityService,
            new FakeBrokerServerProfileService(
                new BrokerServerProfile
                {
                    Id = Guid.NewGuid(),
                    Name = "Active server",
                    Host = "mqtt.meshtastic.org",
                    Port = 1883,
                    DefaultTopicPattern = "msh/US/2/e/LongFast/#",
                    DownlinkTopic = "msh/US/2/json/mqtt/",
                    EnableSend = true,
                    IsActive = true
                }),
            new FakeWorkspaceContextAccessor(),
            Options.Create(
                new BrokerOptions
                {
                    DownlinkTopic = "msh/US/2/json/mqtt/"
                }),
            NullLogger<MessageComposerService>.Instance);
    }

    private sealed class FakeBrokerRuntimeCommandService : IBrokerRuntimeCommandService
    {
        public string? LastPublishedPayload { get; private set; }

        public string? LastPublishedTopic { get; private set; }

        public Task EnsureConnectedAsync(string workspaceId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ReconcileActiveProfileAsync(string workspaceId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ResetAndReconnectActiveProfileAsync(string workspaceId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task PublishAsync(
            string workspaceId,
            string topic,
            string payload,
            CancellationToken cancellationToken = default)
        {
            LastPublishedTopic = topic;
            LastPublishedPayload = payload;
            return Task.CompletedTask;
        }

        public Task SubscribeEphemeralAsync(
            string workspaceId,
            string topicFilter,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task UnsubscribeEphemeralAsync(
            string workspaceId,
            string topicFilter,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeWorkspaceContextAccessor : IWorkspaceContextAccessor
    {
        public string GetWorkspaceId()
        {
            return "workspace-tests";
        }
    }

    private sealed class FakeSendCapabilityService : ISendCapabilityService
    {
        private readonly SendCapabilityStatus _status;

        public FakeSendCapabilityService(SendCapabilityStatus status)
        {
            _status = status;
        }

        public Task<SendCapabilityStatus> GetStatus(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_status);
        }
    }

    private sealed class FakeBrokerServerProfileService : IBrokerServerProfileService
    {
        private readonly BrokerServerProfile _activeProfile;

        public FakeBrokerServerProfileService(BrokerServerProfile activeProfile)
        {
            _activeProfile = activeProfile;
        }

        public Task<IReadOnlyCollection<BrokerServerProfile>> GetServerProfiles(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<BrokerServerProfile>>([_activeProfile]);
        }

        public Task<BrokerServerProfile> GetActiveServerProfile(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_activeProfile);
        }

        public Task<BrokerServerProfile> SaveServerProfile(
            SaveBrokerServerProfileRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<BrokerServerProfile> SetActiveServerProfile(Guid profileId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
