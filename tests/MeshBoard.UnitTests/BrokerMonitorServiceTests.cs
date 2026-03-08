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
        Assert.Equal(
            [
                "msh/US/2/e/LongFast/#",
                "msh/US/2/json/LongFast/#"
            ],
            mqttSession.SubscribedFilters);
    }

    [Fact]
    public async Task UnsubscribeFromTopic_ShouldCallSession_WithTrimmedFilter()
    {
        var mqttSession = new FakeMqttSession(isConnected: true);
        var service = CreateService(mqttSession);

        await service.UnsubscribeFromTopic("  msh/US/2/e/LongFast/#  ");

        Assert.Equal(
            [
                "msh/US/2/e/LongFast/#",
                "msh/US/2/json/LongFast/#"
            ],
            mqttSession.UnsubscribedFilters);
    }

    [Fact]
    public async Task SubscribeToTopic_ShouldThrowBadRequest_WhenFilterIsMissing()
    {
        var service = CreateService(new FakeMqttSession(isConnected: true));

        await Assert.ThrowsAsync<BadRequestException>(() => service.SubscribeToTopic(" "));
    }

    [Fact]
    public void GetBrokerStatus_ShouldNormalizeJsonTopicFilters_ForDisplay()
    {
        var mqttSession = new FakeMqttSession(
            isConnected: true,
            [
                "msh/US/2/e/LongFast/#",
                "msh/US/2/json/LongFast/#",
                "msh/EU_868/2/json/MediumFast/#"
            ]);
        var service = CreateService(mqttSession);

        var status = service.GetBrokerStatus();

        Assert.Equal(
            [
                "msh/EU_868/2/e/MediumFast/#",
                "msh/US/2/e/LongFast/#"
            ],
            status.TopicFilters);
    }

    [Fact]
    public async Task SwitchActiveServerProfile_ShouldResetTopicFilters_AndSubscribeNewDefault()
    {
        var initialProfile = new BrokerServerProfile
        {
            Id = Guid.NewGuid(),
            Name = "EU server",
            Host = "mqtt.eu",
            Port = 1883,
            DefaultTopicPattern = "msh/EU_868/2/e/MediumFast/#",
            DownlinkTopic = "msh/EU_868/2/json/mqtt/",
            EnableSend = true,
            IsActive = true
        };
        var targetProfile = new BrokerServerProfile
        {
            Id = Guid.NewGuid(),
            Name = "US server",
            Host = "mqtt.us",
            Port = 1883,
            DefaultTopicPattern = "msh/US/2/e/LongFast/#",
            DownlinkTopic = "msh/US/2/json/mqtt/",
            EnableSend = true,
            IsActive = false
        };
        var mqttSession = new FakeMqttSession(
            isConnected: true,
            [
                "msh/EU_868/2/e/MediumFast/#",
                "msh/EU_868/2/json/MediumFast/#"
            ]);
        var profileService = new FakeBrokerServerProfileService(initialProfile, targetProfile);
        var service = new BrokerMonitorService(
            mqttSession,
            profileService,
            Options.Create(new BrokerOptions { Host = "mqtt.meshtastic.org", Port = 1883 }),
            new FakeBrokerRuntimeRegistry(),
            NullLogger<BrokerMonitorService>.Instance);

        await service.SwitchActiveServerProfile(targetProfile.Id);

        Assert.Equal(
            [
                "msh/EU_868/2/e/MediumFast/#",
                "msh/EU_868/2/json/MediumFast/#"
            ],
            mqttSession.UnsubscribedFilters);
        Assert.Equal(1, mqttSession.DisconnectCalls);
        Assert.Equal(1, mqttSession.ConnectCalls);
        Assert.Equal(
            [
                "msh/US/2/e/#",
                "msh/US/2/json/#"
            ],
            mqttSession.SubscribedFilters);
    }

    [Fact]
    public async Task GetBrokerStatus_ShouldReadRuntimeSnapshot_FromSharedRegistryAcrossServiceInstances()
    {
        var initialProfile = new BrokerServerProfile
        {
            Id = Guid.NewGuid(),
            Name = "EU server",
            Host = "mqtt.eu",
            Port = 1883,
            DefaultTopicPattern = "msh/EU_868/2/e/MediumFast/#",
            DownlinkTopic = "msh/EU_868/2/json/mqtt/",
            EnableSend = true,
            IsActive = true
        };
        var targetProfile = new BrokerServerProfile
        {
            Id = Guid.NewGuid(),
            Name = "US server",
            Host = "mqtt.us",
            Port = 1883,
            DefaultTopicPattern = "msh/US/2/e/LongFast/#",
            DownlinkTopic = "msh/US/2/json/mqtt/",
            EnableSend = true,
            IsActive = false
        };
        var mqttSession = new FakeMqttSession(isConnected: true);
        var profileService = new FakeBrokerServerProfileService(initialProfile, targetProfile);
        var runtimeRegistry = new FakeBrokerRuntimeRegistry();
        var serviceA = new BrokerMonitorService(
            mqttSession,
            profileService,
            Options.Create(new BrokerOptions { Host = "mqtt.meshtastic.org", Port = 1883 }),
            runtimeRegistry,
            NullLogger<BrokerMonitorService>.Instance);
        var serviceB = new BrokerMonitorService(
            mqttSession,
            profileService,
            Options.Create(new BrokerOptions { Host = "mqtt.meshtastic.org", Port = 1883 }),
            runtimeRegistry,
            NullLogger<BrokerMonitorService>.Instance);

        await serviceA.SwitchActiveServerProfile(targetProfile.Id);

        var status = serviceB.GetBrokerStatus();

        Assert.Equal(targetProfile.Id, status.ActiveServerProfileId);
        Assert.Equal("US server", status.ActiveServerName);
        Assert.Equal("mqtt.us:1883", status.ActiveServerAddress);
    }

    [Fact]
    public async Task EnsureConnected_ShouldNotOverwriteRuntimeSnapshot_WhenSessionIsAlreadyConnected()
    {
        var runtimeRegistry = new FakeBrokerRuntimeRegistry();
        runtimeRegistry.UpdateSnapshot(
            new BrokerRuntimeSnapshot
            {
                ActiveServerProfileId = Guid.NewGuid(),
                ActiveServerName = "Already connected runtime",
                ActiveServerAddress = "mqtt.connected:1883"
            });
        var activeProfile = new BrokerServerProfile
        {
            Id = Guid.NewGuid(),
            Name = "Different workspace profile",
            Host = "mqtt.different",
            Port = 1883,
            DefaultTopicPattern = "msh/US/2/e/LongFast/#",
            DownlinkTopic = "msh/US/2/json/mqtt/",
            EnableSend = true,
            IsActive = true
        };
        var service = new BrokerMonitorService(
            new FakeMqttSession(isConnected: true),
            new FakeBrokerServerProfileService(activeProfile),
            Options.Create(new BrokerOptions { Host = "mqtt.meshtastic.org", Port = 1883 }),
            runtimeRegistry,
            NullLogger<BrokerMonitorService>.Instance);

        await service.EnsureConnected();

        var status = service.GetBrokerStatus();

        Assert.Equal("Already connected runtime", status.ActiveServerName);
        Assert.Equal("mqtt.connected:1883", status.ActiveServerAddress);
    }

    private static BrokerMonitorService CreateService(IMqttSession mqttSession)
    {
        return new BrokerMonitorService(
            mqttSession,
            new FakeBrokerServerProfileService(
                new BrokerServerProfile
                {
                    Id = Guid.NewGuid(),
                    Name = "Default server",
                    Host = "mqtt.meshtastic.org",
                    Port = 1883,
                    DefaultTopicPattern = "msh/US/2/e/LongFast/#",
                    DownlinkTopic = "msh/US/2/json/mqtt/",
                    EnableSend = true,
                    IsActive = true
                }),
            Options.Create(new BrokerOptions { Host = "mqtt.meshtastic.org", Port = 1883 }),
            new FakeBrokerRuntimeRegistry(),
            NullLogger<BrokerMonitorService>.Instance);
    }

    private sealed class FakeBrokerRuntimeRegistry : IBrokerRuntimeRegistry
    {
        private BrokerRuntimeSnapshot _snapshot = new()
        {
            ActiveServerName = "Default server",
            ActiveServerAddress = "mqtt.meshtastic.org:1883"
        };

        public BrokerRuntimeSnapshot GetSnapshot()
        {
            return new BrokerRuntimeSnapshot
            {
                ActiveServerProfileId = _snapshot.ActiveServerProfileId,
                ActiveServerName = _snapshot.ActiveServerName,
                ActiveServerAddress = _snapshot.ActiveServerAddress
            };
        }

        public void UpdateSnapshot(BrokerRuntimeSnapshot snapshot)
        {
            _snapshot = new BrokerRuntimeSnapshot
            {
                ActiveServerProfileId = snapshot.ActiveServerProfileId,
                ActiveServerName = snapshot.ActiveServerName,
                ActiveServerAddress = snapshot.ActiveServerAddress
            };
        }
    }

    private sealed class FakeBrokerServerProfileService : IBrokerServerProfileService
    {
        private BrokerServerProfile _activeProfile;
        private readonly Dictionary<Guid, BrokerServerProfile> _profiles;

        public FakeBrokerServerProfileService(params BrokerServerProfile[] profiles)
        {
            _profiles = profiles.ToDictionary(profile => profile.Id);
            _activeProfile = profiles.First();
        }

        public Task<IReadOnlyCollection<BrokerServerProfile>> GetServerProfiles(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<BrokerServerProfile>>(_profiles.Values.ToList());
        }

        public Task<BrokerServerProfile> GetActiveServerProfile(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_activeProfile);
        }

        public Task<BrokerServerProfile> SaveServerProfile(
            SaveBrokerServerProfileRequest request,
            CancellationToken cancellationToken = default)
        {
            _activeProfile = new BrokerServerProfile
            {
                Id = request.Id is { } existingId && existingId != Guid.Empty
                    ? existingId
                    : Guid.NewGuid(),
                Name = request.Name,
                Host = request.Host,
                Port = request.Port,
                UseTls = request.UseTls,
                Username = request.Username,
                Password = request.Password,
                DefaultTopicPattern = request.DefaultTopicPattern,
                DefaultEncryptionKeyBase64 = request.DefaultEncryptionKeyBase64,
                DownlinkTopic = request.DownlinkTopic,
                EnableSend = request.EnableSend,
                IsActive = request.IsActive
            };
            _profiles[_activeProfile.Id] = _activeProfile;

            return Task.FromResult(_activeProfile);
        }

        public Task<BrokerServerProfile> SetActiveServerProfile(Guid profileId, CancellationToken cancellationToken = default)
        {
            if (_profiles.TryGetValue(profileId, out var selectedProfile))
            {
                _activeProfile = selectedProfile;
            }
            else
            {
                _activeProfile.Id = profileId;
            }

            foreach (var profile in _profiles.Values)
            {
                profile.IsActive = profile.Id == _activeProfile.Id;
            }

            _activeProfile.IsActive = true;
            return Task.FromResult(_activeProfile);
        }
    }

#pragma warning disable CS0067
    private sealed class FakeMqttSession : IMqttSession
    {
        private readonly List<string> _topicFilters;

        public FakeMqttSession(bool isConnected, IEnumerable<string>? topicFilters = null)
        {
            IsConnected = isConnected;
            _topicFilters = topicFilters?.ToList() ?? [];
        }

        public int ConnectCalls { get; private set; }

        public int DisconnectCalls { get; private set; }

        public bool IsConnected { get; private set; }

        public string? LastStatusMessage => null;

        public List<string> SubscribedFilters { get; } = [];

        public List<string> UnsubscribedFilters { get; } = [];

        public IReadOnlyCollection<string> TopicFilters => _topicFilters.ToList();

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
            DisconnectCalls++;
            IsConnected = false;
            return Task.CompletedTask;
        }

        public Task PublishAsync(string topic, string payload, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SubscribeAsync(string topicFilter, CancellationToken cancellationToken = default)
        {
            SubscribedFilters.Add(topicFilter);
            _topicFilters.Add(topicFilter);
            return Task.CompletedTask;
        }

        public Task UnsubscribeAsync(string topicFilter, CancellationToken cancellationToken = default)
        {
            UnsubscribedFilters.Add(topicFilter);
            _topicFilters.RemoveAll(filter => string.Equals(filter, topicFilter, StringComparison.Ordinal));
            return Task.CompletedTask;
        }
    }
#pragma warning restore CS0067
}
