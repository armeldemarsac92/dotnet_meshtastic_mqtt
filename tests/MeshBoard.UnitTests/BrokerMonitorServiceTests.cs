using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Application.Abstractions.Workspaces;
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
    public async Task SubscribeToTopic_ShouldPersistIntent_ConnectAndSubscribe_WithTrimmedFilter()
    {
        var brokerSessionManager = new FakeWorkspaceBrokerSessionManager(isConnected: false);
        var targetProfile = CreateProfile(
            name: "Default server",
            host: "mqtt.meshtastic.org",
            defaultTopicPattern: "msh/US/2/e/LongFast/#");
        var profileService = new FakeBrokerServerProfileService(targetProfile);
        var profileRepository = new FakeBrokerServerProfileRepository();
        var intentRepository = new FakeSubscriptionIntentRepository();
        var service = CreateService(brokerSessionManager, profileService, profileRepository, intentRepository);

        await service.SubscribeToTopic("  msh/US/2/e/LongFast/#  ");

        Assert.Equal(1, brokerSessionManager.ConnectCalls);
        Assert.Equal(
            [
                "msh/US/2/e/LongFast/#",
                "msh/US/2/json/LongFast/#"
            ],
            brokerSessionManager.SubscribedFilters);
        Assert.Equal(["msh/US/2/e/LongFast/#"], intentRepository.GetStoredTopicFilters(targetProfile.Id));
        Assert.True(profileRepository.IsInitialized(targetProfile.Id));
    }

    [Fact]
    public async Task UnsubscribeFromTopic_ShouldRemoveIntent_AndUnsubscribeRuntimeFilters()
    {
        var targetProfile = CreateProfile(
            name: "Default server",
            host: "mqtt.meshtastic.org",
            defaultTopicPattern: "msh/US/2/e/LongFast/#");
        var brokerSessionManager = new FakeWorkspaceBrokerSessionManager(
            isConnected: true,
            [
                "msh/US/2/e/LongFast/#",
                "msh/US/2/json/LongFast/#"
            ]);
        var profileService = new FakeBrokerServerProfileService(targetProfile);
        var profileRepository = new FakeBrokerServerProfileRepository();
        profileRepository.MarkInitialized(targetProfile.Id);
        var intentRepository = new FakeSubscriptionIntentRepository();
        intentRepository.AddStoredIntent(targetProfile.Id, "msh/US/2/e/LongFast/#");
        var service = CreateService(brokerSessionManager, profileService, profileRepository, intentRepository);

        await service.UnsubscribeFromTopic("  msh/US/2/e/LongFast/#  ");

        Assert.Equal(
            [
                "msh/US/2/e/LongFast/#",
                "msh/US/2/json/LongFast/#"
            ],
            brokerSessionManager.UnsubscribedFilters);
        Assert.Empty(intentRepository.GetStoredTopicFilters(targetProfile.Id));
    }

    [Fact]
    public async Task SubscribeToTopic_ShouldThrowBadRequest_WhenFilterIsMissing()
    {
        var service = CreateService(new FakeWorkspaceBrokerSessionManager(isConnected: true));

        await Assert.ThrowsAsync<BadRequestException>(() => service.SubscribeToTopic(" "));
    }

    [Fact]
    public async Task SubscribeToEphemeralTopic_ShouldNotPersistIntent()
    {
        var brokerSessionManager = new FakeWorkspaceBrokerSessionManager(isConnected: true);
        var targetProfile = CreateProfile(
            name: "Default server",
            host: "mqtt.meshtastic.org",
            defaultTopicPattern: "msh/US/2/e/LongFast/#");
        var intentRepository = new FakeSubscriptionIntentRepository();
        var service = CreateService(
            brokerSessionManager,
            new FakeBrokerServerProfileService(targetProfile),
            new FakeBrokerServerProfileRepository(),
            intentRepository);

        await service.SubscribeToEphemeralTopic("msh/US/2/e/LongFast/#");

        Assert.Equal(
            [
                "msh/US/2/e/LongFast/#",
                "msh/US/2/json/LongFast/#"
            ],
            brokerSessionManager.SubscribedFilters);
        Assert.Empty(intentRepository.GetStoredTopicFilters(targetProfile.Id));
    }

    [Fact]
    public void GetBrokerStatus_ShouldNormalizeJsonTopicFilters_ForDisplay()
    {
        var brokerSessionManager = new FakeWorkspaceBrokerSessionManager(
            isConnected: true,
            [
                "msh/US/2/e/LongFast/#",
                "msh/US/2/json/LongFast/#",
                "msh/EU_868/2/json/MediumFast/#"
            ]);
        var service = CreateService(brokerSessionManager);

        var status = service.GetBrokerStatus();

        Assert.Equal(
            [
                "msh/EU_868/2/e/MediumFast/#",
                "msh/US/2/e/LongFast/#"
            ],
            status.TopicFilters);
    }

    [Fact]
    public async Task SwitchActiveServerProfile_ShouldResetTopicFilters_AndSeedDefaultIntent_WhenProfileNotInitialized()
    {
        var initialProfile = CreateProfile(
            name: "EU server",
            host: "mqtt.eu",
            defaultTopicPattern: "msh/EU_868/2/e/MediumFast/#");
        var targetProfile = CreateProfile(
            name: "US server",
            host: "mqtt.us",
            defaultTopicPattern: "msh/US/2/e/LongFast/#");
        var brokerSessionManager = new FakeWorkspaceBrokerSessionManager(
            isConnected: true,
            [
                "msh/EU_868/2/e/MediumFast/#",
                "msh/EU_868/2/json/MediumFast/#"
            ]);
        var profileService = new FakeBrokerServerProfileService(initialProfile, targetProfile);
        var profileRepository = new FakeBrokerServerProfileRepository();
        var intentRepository = new FakeSubscriptionIntentRepository();
        var service = CreateService(brokerSessionManager, profileService, profileRepository, intentRepository);

        await service.SwitchActiveServerProfile(targetProfile.Id);

        Assert.Equal(1, brokerSessionManager.ResetCalls);
        Assert.Equal(1, brokerSessionManager.ConnectCalls);
        Assert.Equal(
            [
                "msh/US/2/e/#",
                "msh/US/2/json/#"
            ],
            brokerSessionManager.SubscribedFilters);
        Assert.Equal(["msh/US/2/e/#"], intentRepository.GetStoredTopicFilters(targetProfile.Id));
        Assert.True(profileRepository.IsInitialized(targetProfile.Id));
    }

    [Fact]
    public async Task GetBrokerStatus_ShouldReadRuntimeSnapshot_FromSharedRegistryAcrossServiceInstances()
    {
        var initialProfile = CreateProfile(
            name: "EU server",
            host: "mqtt.eu",
            defaultTopicPattern: "msh/EU_868/2/e/MediumFast/#");
        var targetProfile = CreateProfile(
            name: "US server",
            host: "mqtt.us",
            defaultTopicPattern: "msh/US/2/e/LongFast/#");
        var brokerSessionManager = new FakeWorkspaceBrokerSessionManager(isConnected: true);
        var profileService = new FakeBrokerServerProfileService(initialProfile, targetProfile);
        var profileRepository = new FakeBrokerServerProfileRepository();
        profileRepository.MarkInitialized(targetProfile.Id);
        var intentRepository = new FakeSubscriptionIntentRepository();
        intentRepository.AddStoredIntent(targetProfile.Id, "msh/US/2/e/LongFast/#");
        var runtimeRegistry = new FakeBrokerRuntimeRegistry();
        var serviceA = CreateService(brokerSessionManager, profileService, profileRepository, intentRepository, runtimeRegistry);
        var serviceB = CreateService(brokerSessionManager, profileService, profileRepository, intentRepository, runtimeRegistry);

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
            "workspace-tests",
            new BrokerRuntimeSnapshot
            {
                ActiveServerProfileId = Guid.NewGuid(),
                ActiveServerName = "Already connected runtime",
                ActiveServerAddress = "mqtt.connected:1883"
            });
        var activeProfile = CreateProfile(
            name: "Different workspace profile",
            host: "mqtt.different",
            defaultTopicPattern: "msh/US/2/e/LongFast/#");
        var service = CreateService(
            new FakeWorkspaceBrokerSessionManager(isConnected: true),
            new FakeBrokerServerProfileService(activeProfile),
            new FakeBrokerServerProfileRepository(),
            new FakeSubscriptionIntentRepository(),
            runtimeRegistry);

        await service.EnsureConnected();

        var status = service.GetBrokerStatus();

        Assert.Equal("Already connected runtime", status.ActiveServerName);
        Assert.Equal("mqtt.connected:1883", status.ActiveServerAddress);
    }

    private static BrokerMonitorService CreateService(
        IWorkspaceBrokerSessionManager brokerSessionManager,
        FakeBrokerServerProfileService? profileService = null,
        FakeBrokerServerProfileRepository? profileRepository = null,
        FakeSubscriptionIntentRepository? intentRepository = null,
        FakeBrokerRuntimeRegistry? runtimeRegistry = null)
    {
        var defaultProfile = CreateProfile(
            name: "Default server",
            host: "mqtt.meshtastic.org",
            defaultTopicPattern: "msh/US/2/e/LongFast/#");

        return new BrokerMonitorService(
            brokerSessionManager,
            profileService ?? new FakeBrokerServerProfileService(defaultProfile),
            profileRepository ?? new FakeBrokerServerProfileRepository(),
            intentRepository ?? new FakeSubscriptionIntentRepository(),
            new FakeWorkspaceContextAccessor(),
            Options.Create(new BrokerOptions { Host = "mqtt.meshtastic.org", Port = 1883 }),
            runtimeRegistry ?? new FakeBrokerRuntimeRegistry(),
            NullLogger<BrokerMonitorService>.Instance);
    }

    private static BrokerServerProfile CreateProfile(string name, string host, string defaultTopicPattern)
    {
        return new BrokerServerProfile
        {
            Id = Guid.NewGuid(),
            Name = name,
            Host = host,
            Port = 1883,
            DefaultTopicPattern = defaultTopicPattern,
            DownlinkTopic = "msh/US/2/json/mqtt/",
            EnableSend = true,
            IsActive = true
        };
    }

    private sealed class FakeWorkspaceContextAccessor : IWorkspaceContextAccessor
    {
        public string GetWorkspaceId()
        {
            return "workspace-tests";
        }
    }

    private sealed class FakeBrokerRuntimeRegistry : IBrokerRuntimeRegistry
    {
        private BrokerRuntimeSnapshot _snapshot = new()
        {
            ActiveServerName = "Default server",
            ActiveServerAddress = "mqtt.meshtastic.org:1883"
        };

        public BrokerRuntimeSnapshot GetSnapshot(string workspaceId)
        {
            return new BrokerRuntimeSnapshot
            {
                ActiveServerProfileId = _snapshot.ActiveServerProfileId,
                ActiveServerName = _snapshot.ActiveServerName,
                ActiveServerAddress = _snapshot.ActiveServerAddress
            };
        }

        public void UpdateSnapshot(string workspaceId, BrokerRuntimeSnapshot snapshot)
        {
            _snapshot = new BrokerRuntimeSnapshot
            {
                ActiveServerProfileId = snapshot.ActiveServerProfileId,
                ActiveServerName = snapshot.ActiveServerName,
                ActiveServerAddress = snapshot.ActiveServerAddress
            };
        }
    }

    private sealed class FakeSubscriptionIntentRepository : ISubscriptionIntentRepository
    {
        private readonly Dictionary<Guid, HashSet<string>> _topicFiltersByProfile = [];

        public Task<IReadOnlyCollection<SubscriptionIntent>> GetAllAsync(
            string workspaceId,
            Guid brokerServerProfileId,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyCollection<SubscriptionIntent> results = GetStoredTopicFilters(brokerServerProfileId)
                .Select(
                    topicFilter => new SubscriptionIntent
                    {
                        BrokerServerProfileId = brokerServerProfileId,
                        TopicFilter = topicFilter,
                        CreatedAtUtc = DateTimeOffset.UtcNow
                    })
                .ToList();

            return Task.FromResult(results);
        }

        public Task<bool> AddAsync(
            string workspaceId,
            Guid brokerServerProfileId,
            string topicFilter,
            CancellationToken cancellationToken = default)
        {
            if (!_topicFiltersByProfile.TryGetValue(brokerServerProfileId, out var topicFilters))
            {
                topicFilters = new HashSet<string>(StringComparer.Ordinal);
                _topicFiltersByProfile[brokerServerProfileId] = topicFilters;
            }

            return Task.FromResult(topicFilters.Add(topicFilter));
        }

        public Task<bool> DeleteAsync(
            string workspaceId,
            Guid brokerServerProfileId,
            string topicFilter,
            CancellationToken cancellationToken = default)
        {
            if (!_topicFiltersByProfile.TryGetValue(brokerServerProfileId, out var topicFilters))
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(topicFilters.Remove(topicFilter));
        }

        public IReadOnlyCollection<string> GetStoredTopicFilters(Guid brokerServerProfileId)
        {
            return _topicFiltersByProfile.TryGetValue(brokerServerProfileId, out var topicFilters)
                ? topicFilters.OrderBy(topicFilter => topicFilter, StringComparer.Ordinal).ToList()
                : [];
        }

        public void AddStoredIntent(Guid brokerServerProfileId, string topicFilter)
        {
            if (!_topicFiltersByProfile.TryGetValue(brokerServerProfileId, out var topicFilters))
            {
                topicFilters = new HashSet<string>(StringComparer.Ordinal);
                _topicFiltersByProfile[brokerServerProfileId] = topicFilters;
            }

            topicFilters.Add(topicFilter);
        }
    }

    private sealed class FakeBrokerServerProfileRepository : IBrokerServerProfileRepository
    {
        private readonly HashSet<Guid> _initializedProfiles = [];

        public Task<IReadOnlyCollection<WorkspaceBrokerServerProfile>> GetAllActiveAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<WorkspaceBrokerServerProfile>>([]);
        }

        public Task<IReadOnlyCollection<BrokerServerProfile>> GetAllAsync(
            string workspaceId,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyCollection<BrokerServerProfile> profiles = [];
            return Task.FromResult(profiles);
        }

        public Task<BrokerServerProfile?> GetActiveAsync(
            string workspaceId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<BrokerServerProfile?>(null);
        }

        public Task<BrokerServerProfile?> GetByIdAsync(
            string workspaceId,
            Guid id,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<BrokerServerProfile?>(null);
        }

        public Task SetExclusiveActiveAsync(
            string workspaceId,
            Guid id,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<bool> AreSubscriptionIntentsInitializedAsync(
            string workspaceId,
            Guid id,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_initializedProfiles.Contains(id));
        }

        public Task MarkSubscriptionIntentsInitializedAsync(
            string workspaceId,
            Guid id,
            CancellationToken cancellationToken = default)
        {
            _initializedProfiles.Add(id);
            return Task.CompletedTask;
        }

        public Task<BrokerServerProfile> UpsertAsync(
            string workspaceId,
            SaveBrokerServerProfileRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public bool IsInitialized(Guid profileId)
        {
            return _initializedProfiles.Contains(profileId);
        }

        public void MarkInitialized(Guid profileId)
        {
            _initializedProfiles.Add(profileId);
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
            throw new NotSupportedException();
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
    private sealed class FakeWorkspaceBrokerSessionManager : IWorkspaceBrokerSessionManager
    {
        private readonly List<string> _topicFilters;

        public FakeWorkspaceBrokerSessionManager(bool isConnected, IEnumerable<string>? topicFilters = null)
        {
            _isConnected = isConnected;
            _topicFilters = topicFilters?.ToList() ?? [];
        }

        private bool _isConnected;

        public int ConnectCalls { get; private set; }

        public int DisconnectCalls { get; private set; }

        public int ResetCalls { get; private set; }

        public List<string> SubscribedFilters { get; } = [];

        public List<string> UnsubscribedFilters { get; } = [];

        public event Func<MqttInboundMessage, Task>? MessageReceived;

        public bool IsConnected(string workspaceId)
        {
            return _isConnected;
        }

        public string? GetLastStatusMessage(string workspaceId)
        {
            return null;
        }

        public IReadOnlyCollection<string> GetTopicFilters(string workspaceId)
        {
            return _topicFilters.ToList();
        }

        public Task ConnectAsync(string workspaceId, CancellationToken cancellationToken = default)
        {
            ConnectCalls++;
            _isConnected = true;
            return Task.CompletedTask;
        }

        public Task ResetRuntimeAsync(string workspaceId, CancellationToken cancellationToken = default)
        {
            ResetCalls++;
            DisconnectCalls++;
            _isConnected = false;
            _topicFilters.Clear();
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(string workspaceId, CancellationToken cancellationToken = default)
        {
            DisconnectCalls++;
            _isConnected = false;
            return Task.CompletedTask;
        }

        public Task DisconnectAllAsync(CancellationToken cancellationToken = default)
        {
            _isConnected = false;
            _topicFilters.Clear();
            return Task.CompletedTask;
        }

        public Task PublishAsync(string workspaceId, string topic, string payload, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SubscribeAsync(string workspaceId, string topicFilter, CancellationToken cancellationToken = default)
        {
            SubscribedFilters.Add(topicFilter);

            if (!_topicFilters.Contains(topicFilter, StringComparer.Ordinal))
            {
                _topicFilters.Add(topicFilter);
            }

            return Task.CompletedTask;
        }

        public Task UnsubscribeAsync(string workspaceId, string topicFilter, CancellationToken cancellationToken = default)
        {
            UnsubscribedFilters.Add(topicFilter);
            _topicFilters.RemoveAll(filter => string.Equals(filter, topicFilter, StringComparison.Ordinal));
            return Task.CompletedTask;
        }
    }
#pragma warning restore CS0067
}
