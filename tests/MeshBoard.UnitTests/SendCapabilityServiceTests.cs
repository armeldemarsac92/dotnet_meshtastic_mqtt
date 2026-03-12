using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Application.Abstractions.Workspaces;
using MeshBoard.Application.Services;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Exceptions;
using MeshBoard.Contracts.Meshtastic;
using Microsoft.Extensions.Options;

namespace MeshBoard.UnitTests;

public sealed class SendCapabilityServiceTests
{
    [Fact]
    public async Task GetStatus_ShouldBeBlocked_WhenSendDisabled()
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

        var status = await service.GetStatus();

        Assert.False(status.IsEnabled);
        Assert.Contains(status.BlockingReasons, reason => reason.Contains("disabled by configuration", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetStatus_ShouldBeBlocked_WhenMqttDisconnected()
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

        var status = await service.GetStatus();

        Assert.False(status.IsEnabled);
        Assert.Contains(status.BlockingReasons, reason => reason.Contains("not connected", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetStatus_ShouldBeEnabled_WhenPreconditionsAreMet()
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

        var status = await service.GetStatus();

        Assert.True(status.IsEnabled);
        Assert.Empty(status.BlockingReasons);
        Assert.Equal("mqtt.meshtastic.org", status.Host);
        Assert.Equal(1883, status.Port);
    }

    [Fact]
    public async Task GetStatus_ShouldFallBackToBrokerOptions_WhenNoActiveProfileExists()
    {
        var service = new SendCapabilityService(
            Options.Create(
                new BrokerOptions
                {
                    Host = "mqtt-fallback.example.org",
                    Port = 1884,
                    EnableSend = true,
                    DownlinkTopic = "msh/EU_868/2/json/mqtt/"
                }),
            new FakeBrokerRuntimeRegistry(isConnected: true),
            new ThrowingBrokerServerProfileService(new NotFoundException("No active broker server profile is configured.")),
            new FakeWorkspaceContextAccessor());

        var status = await service.GetStatus();

        Assert.True(status.IsEnabled);
        Assert.Equal("mqtt-fallback.example.org", status.Host);
        Assert.Equal(1884, status.Port);
        Assert.Equal("msh/EU_868/2/json/mqtt/", status.DownlinkTopic);
    }

    [Fact]
    public async Task GetStatus_ShouldPropagateUnexpectedErrors_WhenActiveProfileLookupFails()
    {
        var service = new SendCapabilityService(
            Options.Create(
                new BrokerOptions
                {
                    Host = "mqtt.meshtastic.org",
                    Port = 1883,
                    EnableSend = true,
                    DownlinkTopic = "msh/US/2/json/mqtt/"
                }),
            new FakeBrokerRuntimeRegistry(isConnected: true),
            new ThrowingBrokerServerProfileService(new InvalidOperationException("database unavailable")),
            new FakeWorkspaceContextAccessor());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetStatus());

        Assert.Equal("database unavailable", exception.Message);
    }

    private static SendCapabilityService CreateService(BrokerOptions options, bool isConnected)
    {
        return new SendCapabilityService(
            Options.Create(options),
            new FakeBrokerRuntimeRegistry(isConnected),
            new FakeBrokerServerProfileService(
                new BrokerServerProfile
                {
                    Id = Guid.NewGuid(),
                    Name = "Active server",
                    Host = options.Host,
                    Port = options.Port,
                    DefaultTopicPattern = options.DefaultTopicPattern,
                    DefaultEncryptionKeyBase64 = options.DefaultEncryptionKeyBase64,
                    DownlinkTopic = options.DownlinkTopic,
                    EnableSend = options.EnableSend,
                    IsActive = true
                }),
            new FakeWorkspaceContextAccessor());
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

        public Task<BrokerServerProfile?> GetServerProfileById(Guid profileId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<BrokerServerProfile?>(_activeProfile.Id == profileId ? _activeProfile : null);
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

    private sealed class ThrowingBrokerServerProfileService : IBrokerServerProfileService
    {
        private readonly Exception _exception;

        public ThrowingBrokerServerProfileService(Exception exception)
        {
            _exception = exception;
        }

        public Task<IReadOnlyCollection<BrokerServerProfile>> GetServerProfiles(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<BrokerServerProfile> GetActiveServerProfile(CancellationToken cancellationToken = default)
        {
            return Task.FromException<BrokerServerProfile>(_exception);
        }

        public Task<BrokerServerProfile?> GetServerProfileById(Guid profileId, CancellationToken cancellationToken = default)
        {
            return Task.FromException<BrokerServerProfile?>(_exception);
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

    private sealed class FakeBrokerRuntimeRegistry : IBrokerRuntimeRegistry
    {
        private readonly BrokerRuntimeSnapshot _snapshot;

        public FakeBrokerRuntimeRegistry(bool isConnected)
        {
            _snapshot = new BrokerRuntimeSnapshot { IsConnected = isConnected };
        }

        public BrokerRuntimeSnapshot GetSnapshot(string workspaceId)
        {
            return new BrokerRuntimeSnapshot
            {
                IsConnected = _snapshot.IsConnected,
                TopicFilters = [.._snapshot.TopicFilters]
            };
        }

        public void UpdateSnapshot(string workspaceId, BrokerRuntimeSnapshot snapshot)
        {
            throw new NotSupportedException();
        }

        public RuntimePipelineSnapshot GetPipelineSnapshot(string workspaceId)
        {
            return new RuntimePipelineSnapshot();
        }

        public void UpdatePipelineSnapshot(string workspaceId, RuntimePipelineSnapshot snapshot)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeWorkspaceContextAccessor : IWorkspaceContextAccessor
    {
        public string GetWorkspaceId()
        {
            return "workspace-tests";
        }
    }
}
