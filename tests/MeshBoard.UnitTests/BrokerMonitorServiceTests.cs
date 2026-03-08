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
    public async Task SubscribeToTopic_ShouldPersistIntent_AndReconcileActiveProfile_WithTrimmedFilter()
    {
        var targetProfile = CreateProfile(
            name: "Default server",
            host: "mqtt.meshtastic.org",
            defaultTopicPattern: "msh/US/2/e/LongFast/#");
        var profileService = new FakeBrokerServerProfileService(targetProfile);
        var intentRepository = new FakeSubscriptionIntentRepository();
        var runtimeCommandService = new FakeBrokerRuntimeCommandService();
        var service = CreateService(runtimeCommandService, profileService, intentRepository);

        await service.SubscribeToTopic("  msh/US/2/e/LongFast/#  ");

        Assert.Equal(["workspace-tests"], runtimeCommandService.ReconcileActiveProfileCalls);
        Assert.Equal(["msh/US/2/e/LongFast/#"], intentRepository.GetStoredTopicFilters(targetProfile.Id));
    }

    [Fact]
    public async Task UnsubscribeFromTopic_ShouldRemoveIntent_AndReconcileActiveProfile()
    {
        var targetProfile = CreateProfile(
            name: "Default server",
            host: "mqtt.meshtastic.org",
            defaultTopicPattern: "msh/US/2/e/LongFast/#");
        var profileService = new FakeBrokerServerProfileService(targetProfile);
        var intentRepository = new FakeSubscriptionIntentRepository();
        intentRepository.AddStoredIntent(targetProfile.Id, "msh/US/2/e/LongFast/#");
        var runtimeCommandService = new FakeBrokerRuntimeCommandService();
        var service = CreateService(runtimeCommandService, profileService, intentRepository);

        await service.UnsubscribeFromTopic("  msh/US/2/e/LongFast/#  ");

        Assert.Equal(["workspace-tests"], runtimeCommandService.ReconcileActiveProfileCalls);
        Assert.Empty(intentRepository.GetStoredTopicFilters(targetProfile.Id));
    }

    [Fact]
    public async Task SubscribeToTopic_ShouldThrowBadRequest_WhenFilterIsMissing()
    {
        var service = CreateService(new FakeBrokerRuntimeCommandService());

        await Assert.ThrowsAsync<BadRequestException>(() => service.SubscribeToTopic(" "));
    }

    [Fact]
    public async Task SubscribeToEphemeralTopic_ShouldNotPersistIntent_AndDelegateToRuntimeCommand()
    {
        var targetProfile = CreateProfile(
            name: "Default server",
            host: "mqtt.meshtastic.org",
            defaultTopicPattern: "msh/US/2/e/LongFast/#");
        var intentRepository = new FakeSubscriptionIntentRepository();
        var runtimeCommandService = new FakeBrokerRuntimeCommandService();
        var service = CreateService(
            runtimeCommandService,
            new FakeBrokerServerProfileService(targetProfile),
            intentRepository: intentRepository);

        await service.SubscribeToEphemeralTopic("msh/US/2/e/LongFast/#");

        Assert.Equal(["msh/US/2/e/LongFast/#"], runtimeCommandService.SubscribeEphemeralFilters);
        Assert.Empty(intentRepository.GetStoredTopicFilters(targetProfile.Id));
    }

    [Fact]
    public void GetBrokerStatus_ShouldReadRuntimeSnapshot_FromRegistry()
    {
        var runtimeRegistry = new FakeBrokerRuntimeRegistry();
        runtimeRegistry.UpdateSnapshot(
            "workspace-tests",
            new BrokerRuntimeSnapshot
            {
                ActiveServerProfileId = Guid.NewGuid(),
                ActiveServerName = "Runtime server",
                ActiveServerAddress = "mqtt.runtime:1883",
                IsConnected = true,
                LastStatusMessage = "Connected",
                TopicFilters = ["msh/EU_868/2/e/MediumFast/#", "msh/US/2/e/LongFast/#"]
            });
        runtimeRegistry.UpdatePipelineSnapshot(
            new RuntimePipelineSnapshot
            {
                InboundQueueCapacity = 2048,
                InboundWorkerCount = 2,
                InboundQueueDepth = 17,
                InboundOldestMessageAgeMilliseconds = 1250,
                InboundEnqueuedCount = 100,
                InboundDequeuedCount = 83,
                InboundDroppedCount = 4,
                UpdatedAtUtc = new DateTimeOffset(2026, 3, 8, 16, 30, 0, TimeSpan.Zero)
            });
        var service = CreateService(
            new FakeBrokerRuntimeCommandService(),
            runtimeRegistry: runtimeRegistry);

        var status = service.GetBrokerStatus();

        Assert.Equal("Runtime server", status.ActiveServerName);
        Assert.Equal("mqtt.runtime:1883", status.ActiveServerAddress);
        Assert.True(status.IsConnected);
        Assert.Equal("Connected", status.LastStatusMessage);
        Assert.Equal(["msh/EU_868/2/e/MediumFast/#", "msh/US/2/e/LongFast/#"], status.TopicFilters);
        Assert.Equal(2048, status.InboundQueueCapacity);
        Assert.Equal(2, status.InboundWorkerCount);
        Assert.Equal(17, status.InboundQueueDepth);
        Assert.Equal(1250, status.InboundOldestMessageAgeMilliseconds);
        Assert.Equal(100, status.InboundEnqueuedCount);
        Assert.Equal(83, status.InboundDequeuedCount);
        Assert.Equal(4, status.InboundDroppedCount);
    }

    [Fact]
    public async Task SwitchActiveServerProfile_ShouldReconnectActiveProfileThroughRuntimeCommand()
    {
        var initialProfile = CreateProfile(
            name: "EU server",
            host: "mqtt.eu",
            defaultTopicPattern: "msh/EU_868/2/e/MediumFast/#");
        var targetProfile = CreateProfile(
            name: "US server",
            host: "mqtt.us",
            defaultTopicPattern: "msh/US/2/e/LongFast/#");
        var profileService = new FakeBrokerServerProfileService(initialProfile, targetProfile);
        var runtimeCommandService = new FakeBrokerRuntimeCommandService();
        var service = CreateService(runtimeCommandService, profileService);

        await service.SwitchActiveServerProfile(targetProfile.Id);

        Assert.Equal(["workspace-tests"], runtimeCommandService.ResetAndReconnectActiveProfileCalls);
        Assert.Equal(targetProfile.Id, profileService.CurrentActiveProfile.Id);
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
        var profileService = new FakeBrokerServerProfileService(initialProfile, targetProfile);
        var runtimeRegistry = new FakeBrokerRuntimeRegistry();
        var runtimeCommandService = new FakeBrokerRuntimeCommandService(runtimeRegistry)
        {
            SnapshotOnResetAndReconnect = new BrokerRuntimeSnapshot
            {
                ActiveServerProfileId = targetProfile.Id,
                ActiveServerName = targetProfile.Name,
                ActiveServerAddress = targetProfile.ServerAddress,
                IsConnected = true,
                TopicFilters = ["msh/US/2/e/LongFast/#"]
            }
        };
        var serviceA = CreateService(runtimeCommandService, profileService, runtimeRegistry: runtimeRegistry);
        var serviceB = CreateService(runtimeCommandService, profileService, runtimeRegistry: runtimeRegistry);

        await serviceA.SwitchActiveServerProfile(targetProfile.Id);

        var status = serviceB.GetBrokerStatus();

        Assert.Equal(targetProfile.Id, status.ActiveServerProfileId);
        Assert.Equal("US server", status.ActiveServerName);
        Assert.Equal("mqtt.us:1883", status.ActiveServerAddress);
    }

    [Fact]
    public async Task EnsureConnected_ShouldDelegateToRuntimeCommand()
    {
        var runtimeCommandService = new FakeBrokerRuntimeCommandService();
        var service = CreateService(runtimeCommandService);

        await service.EnsureConnected();

        Assert.Equal(["workspace-tests"], runtimeCommandService.EnsureConnectedCalls);
    }

    private static BrokerMonitorService CreateService(
        FakeBrokerRuntimeCommandService runtimeCommandService,
        FakeBrokerServerProfileService? profileService = null,
        FakeSubscriptionIntentRepository? intentRepository = null,
        FakeBrokerRuntimeRegistry? runtimeRegistry = null)
    {
        var defaultProfile = CreateProfile(
            name: "Default server",
            host: "mqtt.meshtastic.org",
            defaultTopicPattern: "msh/US/2/e/LongFast/#");

        return new BrokerMonitorService(
            runtimeCommandService,
            profileService ?? new FakeBrokerServerProfileService(defaultProfile),
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
            ActiveServerAddress = "mqtt.meshtastic.org:1883",
            TopicFilters = []
        };
        private RuntimePipelineSnapshot _pipelineSnapshot = new();

        public BrokerRuntimeSnapshot GetSnapshot(string workspaceId)
        {
            return new BrokerRuntimeSnapshot
            {
                ActiveServerProfileId = _snapshot.ActiveServerProfileId,
                ActiveServerName = _snapshot.ActiveServerName,
                ActiveServerAddress = _snapshot.ActiveServerAddress,
                IsConnected = _snapshot.IsConnected,
                LastStatusMessage = _snapshot.LastStatusMessage,
                TopicFilters = [.._snapshot.TopicFilters]
            };
        }

        public void UpdateSnapshot(string workspaceId, BrokerRuntimeSnapshot snapshot)
        {
            _snapshot = new BrokerRuntimeSnapshot
            {
                ActiveServerProfileId = snapshot.ActiveServerProfileId,
                ActiveServerName = snapshot.ActiveServerName,
                ActiveServerAddress = snapshot.ActiveServerAddress,
                IsConnected = snapshot.IsConnected,
                LastStatusMessage = snapshot.LastStatusMessage,
                TopicFilters = [..snapshot.TopicFilters]
            };
        }

        public RuntimePipelineSnapshot GetPipelineSnapshot()
        {
            return new RuntimePipelineSnapshot
            {
                InboundQueueCapacity = _pipelineSnapshot.InboundQueueCapacity,
                InboundWorkerCount = _pipelineSnapshot.InboundWorkerCount,
                InboundQueueDepth = _pipelineSnapshot.InboundQueueDepth,
                InboundOldestMessageAgeMilliseconds = _pipelineSnapshot.InboundOldestMessageAgeMilliseconds,
                InboundEnqueuedCount = _pipelineSnapshot.InboundEnqueuedCount,
                InboundDequeuedCount = _pipelineSnapshot.InboundDequeuedCount,
                InboundDroppedCount = _pipelineSnapshot.InboundDroppedCount,
                UpdatedAtUtc = _pipelineSnapshot.UpdatedAtUtc
            };
        }

        public void UpdatePipelineSnapshot(RuntimePipelineSnapshot snapshot)
        {
            _pipelineSnapshot = new RuntimePipelineSnapshot
            {
                InboundQueueCapacity = snapshot.InboundQueueCapacity,
                InboundWorkerCount = snapshot.InboundWorkerCount,
                InboundQueueDepth = snapshot.InboundQueueDepth,
                InboundOldestMessageAgeMilliseconds = snapshot.InboundOldestMessageAgeMilliseconds,
                InboundEnqueuedCount = snapshot.InboundEnqueuedCount,
                InboundDequeuedCount = snapshot.InboundDequeuedCount,
                InboundDroppedCount = snapshot.InboundDroppedCount,
                UpdatedAtUtc = snapshot.UpdatedAtUtc
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

    private sealed class FakeBrokerServerProfileService : IBrokerServerProfileService
    {
        private BrokerServerProfile _activeProfile;
        private readonly Dictionary<Guid, BrokerServerProfile> _profiles;

        public FakeBrokerServerProfileService(params BrokerServerProfile[] profiles)
        {
            _profiles = profiles.ToDictionary(profile => profile.Id);
            _activeProfile = profiles.First();
        }

        public BrokerServerProfile CurrentActiveProfile => _activeProfile;

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

    private sealed class FakeBrokerRuntimeCommandService : IBrokerRuntimeCommandService
    {
        private readonly FakeBrokerRuntimeRegistry? _runtimeRegistry;

        public FakeBrokerRuntimeCommandService(FakeBrokerRuntimeRegistry? runtimeRegistry = null)
        {
            _runtimeRegistry = runtimeRegistry;
        }

        public List<string> EnsureConnectedCalls { get; } = [];

        public List<string> ReconcileActiveProfileCalls { get; } = [];

        public List<string> ResetAndReconnectActiveProfileCalls { get; } = [];

        public List<string> SubscribeEphemeralFilters { get; } = [];

        public List<string> UnsubscribeEphemeralFilters { get; } = [];

        public BrokerRuntimeSnapshot? SnapshotOnResetAndReconnect { get; set; }

        public Task EnsureConnectedAsync(string workspaceId, CancellationToken cancellationToken = default)
        {
            EnsureConnectedCalls.Add(workspaceId);
            return Task.CompletedTask;
        }

        public Task ReconcileActiveProfileAsync(string workspaceId, CancellationToken cancellationToken = default)
        {
            ReconcileActiveProfileCalls.Add(workspaceId);
            return Task.CompletedTask;
        }

        public Task ResetAndReconnectActiveProfileAsync(string workspaceId, CancellationToken cancellationToken = default)
        {
            ResetAndReconnectActiveProfileCalls.Add(workspaceId);

            if (SnapshotOnResetAndReconnect is not null)
            {
                _runtimeRegistry?.UpdateSnapshot(workspaceId, SnapshotOnResetAndReconnect);
            }

            return Task.CompletedTask;
        }

        public Task PublishAsync(
            string workspaceId,
            string topic,
            string payload,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SubscribeEphemeralAsync(
            string workspaceId,
            string topicFilter,
            CancellationToken cancellationToken = default)
        {
            SubscribeEphemeralFilters.Add(topicFilter);
            return Task.CompletedTask;
        }

        public Task UnsubscribeEphemeralAsync(
            string workspaceId,
            string topicFilter,
            CancellationToken cancellationToken = default)
        {
            UnsubscribeEphemeralFilters.Add(topicFilter);
            return Task.CompletedTask;
        }
    }
}
