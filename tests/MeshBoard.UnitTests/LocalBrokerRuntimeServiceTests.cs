using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Meshtastic;
using MeshBoard.Infrastructure.Meshtastic.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeshBoard.UnitTests;

public sealed class LocalBrokerRuntimeServiceTests
{
    [Fact]
    public async Task ReconcileActiveProfileAsync_ShouldAlwaysSubscribeToMshRoot()
    {
        const string workspaceId = "workspace-a";
        var activeProfile = CreateProfile(
            id: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            host: "mqtt.meshtastic.org");
        var sessionManager = new FakeWorkspaceBrokerSessionManager();
        var service = new LocalBrokerRuntimeService(
            CreateScopeFactory(new FakeBrokerServerProfileRepository(workspaceId, activeProfile)),
            sessionManager,
            new FakeBrokerRuntimeRegistry(),
            NullLogger<LocalBrokerRuntimeService>.Instance);

        await service.ReconcileActiveProfileAsync(workspaceId);

        Assert.Equal(1, sessionManager.ConnectCallCount);
        Assert.Contains("msh/+/2/e/#", sessionManager.GetTopicFilters(workspaceId));
        Assert.Contains("msh/+/2/json/#", sessionManager.GetTopicFilters(workspaceId));
    }

    [Fact]
    public async Task EnsureConnectedAsync_ShouldResetRuntime_WhenActiveServerChanges()
    {
        const string workspaceId = "workspace-a";
        var previousProfile = CreateProfile(
            id: Guid.Parse("33333333-3333-3333-3333-333333333333"),
            host: "mqtt-old.example.org");
        var nextProfile = CreateProfile(
            id: Guid.Parse("44444444-4444-4444-4444-444444444444"),
            host: "mqtt-new.example.org");
        var profileRepository = new FakeBrokerServerProfileRepository(workspaceId, nextProfile);
        var sessionManager = new FakeWorkspaceBrokerSessionManager();
        sessionManager.SetConnected(workspaceId, true);
        sessionManager.SetTopicFilters(workspaceId, ["msh/+/2/e/#", "msh/+/2/json/#"]);
        var runtimeRegistry = new FakeBrokerRuntimeRegistry();
        runtimeRegistry.UpdateSnapshot(
            workspaceId,
            new BrokerRuntimeSnapshot
            {
                ActiveServerProfileId = previousProfile.Id,
                ActiveServerName = previousProfile.Name,
                ActiveServerAddress = previousProfile.ServerAddress,
                IsConnected = true,
                TopicFilters = ["msh/+/2/e/#", "msh/+/2/json/#"]
            });
        var service = new LocalBrokerRuntimeService(
            CreateScopeFactory(profileRepository),
            sessionManager,
            runtimeRegistry,
            NullLogger<LocalBrokerRuntimeService>.Instance);

        await service.EnsureConnectedAsync(workspaceId);

        Assert.Equal(1, sessionManager.ResetRuntimeCallCount);
        Assert.Equal(1, sessionManager.ConnectCallCount);
        Assert.Contains("msh/+/2/e/#", sessionManager.GetTopicFilters(workspaceId));
        Assert.Contains("msh/+/2/json/#", sessionManager.GetTopicFilters(workspaceId));
        Assert.Equal(nextProfile.Id, runtimeRegistry.GetSnapshot(workspaceId).ActiveServerProfileId);
        Assert.Equal(nextProfile.ServerAddress, runtimeRegistry.GetSnapshot(workspaceId).ActiveServerAddress);
    }

    private static BrokerServerProfile CreateProfile(Guid id, string host)
    {
        return new BrokerServerProfile
        {
            Id = id,
            Name = $"Server-{host}",
            Host = host,
            Port = 1883,
            UseTls = false,
            Username = string.Empty,
            Password = string.Empty,
            DownlinkTopic = "msh/US/2/json/mqtt/",
            EnableSend = true,
            IsActive = true
        };
    }

    private static IServiceScopeFactory CreateScopeFactory(
        FakeBrokerServerProfileRepository brokerServerProfileRepository)
    {
        var services = new ServiceCollection();
        services.AddScoped<IBrokerServerProfileRepository>(_ => brokerServerProfileRepository);
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private sealed class FakeBrokerServerProfileRepository : IBrokerServerProfileRepository
    {
        private readonly string _workspaceId;
        private BrokerServerProfile _activeProfile;

        public FakeBrokerServerProfileRepository(string workspaceId, BrokerServerProfile activeProfile)
        {
            _workspaceId = workspaceId;
            _activeProfile = activeProfile;
        }

        public Task<IReadOnlyCollection<WorkspaceBrokerServerProfile>> GetAllActiveAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyCollection<WorkspaceBrokerServerProfile> profiles =
            [
                new WorkspaceBrokerServerProfile
                {
                    WorkspaceId = _workspaceId,
                    Profile = _activeProfile
                }
            ];

            return Task.FromResult(profiles);
        }

        public Task<IReadOnlyCollection<WorkspaceBrokerServerProfile>> GetAllActiveUserOwnedAsync(CancellationToken cancellationToken = default)
        {
            return GetAllActiveAsync(cancellationToken);
        }

        public Task<IReadOnlyCollection<BrokerServerProfile>> GetAllAsync(string workspaceId, CancellationToken cancellationToken = default)
        {
            IReadOnlyCollection<BrokerServerProfile> profiles = workspaceId == _workspaceId ? [_activeProfile] : [];
            return Task.FromResult(profiles);
        }

        public Task<BrokerServerProfile?> GetActiveAsync(string workspaceId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(workspaceId == _workspaceId ? _activeProfile : null);
        }

        public Task<BrokerServerProfile?> GetByIdAsync(string workspaceId, Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(workspaceId == _workspaceId && _activeProfile.Id == id ? _activeProfile : null);
        }

        public Task SetExclusiveActiveAsync(string workspaceId, Guid id, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<BrokerServerProfile> UpsertAsync(string workspaceId, SaveBrokerServerProfileRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeWorkspaceBrokerSessionManager : IWorkspaceBrokerSessionManager
    {
        private readonly Dictionary<string, bool> _connectionsByWorkspace = new(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> _topicFiltersByWorkspace = new(StringComparer.Ordinal);

        public int ConnectCallCount { get; private set; }

        public int ResetRuntimeCallCount { get; private set; }

        public bool IsConnected(string workspaceId)
        {
            return _connectionsByWorkspace.TryGetValue(workspaceId, out var isConnected) && isConnected;
        }

        public string? GetLastStatusMessage(string workspaceId) => null;

        public IReadOnlyCollection<string> GetTopicFilters(string workspaceId)
        {
            return _topicFiltersByWorkspace.TryGetValue(workspaceId, out var filters)
                ? filters.ToArray()
                : [];
        }

#pragma warning disable CS0067
        public event Func<MqttInboundMessage, Task>? MessageReceived;
#pragma warning restore CS0067

        public Task ResetRuntimeAsync(string workspaceId, CancellationToken cancellationToken = default)
        {
            ResetRuntimeCallCount += 1;
            _connectionsByWorkspace[workspaceId] = false;
            _topicFiltersByWorkspace.Remove(workspaceId);
            return Task.CompletedTask;
        }

        public Task ConnectAsync(string workspaceId, CancellationToken cancellationToken = default)
        {
            ConnectCallCount += 1;
            _connectionsByWorkspace[workspaceId] = true;
            _topicFiltersByWorkspace.TryAdd(workspaceId, new HashSet<string>(StringComparer.Ordinal));
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(string workspaceId, CancellationToken cancellationToken = default)
        {
            _connectionsByWorkspace[workspaceId] = false;
            return Task.CompletedTask;
        }

        public Task DisconnectAllAsync(CancellationToken cancellationToken = default)
        {
            _connectionsByWorkspace.Clear();
            _topicFiltersByWorkspace.Clear();
            return Task.CompletedTask;
        }

        public Task PublishAsync(string workspaceId, string topic, string payload, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SubscribeAsync(string workspaceId, string topicFilter, CancellationToken cancellationToken = default)
        {
            _topicFiltersByWorkspace.TryAdd(workspaceId, new HashSet<string>(StringComparer.Ordinal));
            _topicFiltersByWorkspace[workspaceId].Add(topicFilter);
            return Task.CompletedTask;
        }

        public Task UnsubscribeAsync(string workspaceId, string topicFilter, CancellationToken cancellationToken = default)
        {
            if (_topicFiltersByWorkspace.TryGetValue(workspaceId, out var filters))
            {
                filters.Remove(topicFilter);
            }

            return Task.CompletedTask;
        }

        public void SetConnected(string workspaceId, bool isConnected)
        {
            _connectionsByWorkspace[workspaceId] = isConnected;
        }

        public void SetTopicFilters(string workspaceId, IReadOnlyCollection<string> topicFilters)
        {
            _topicFiltersByWorkspace[workspaceId] = topicFilters.ToHashSet(StringComparer.Ordinal);
        }
    }

    private sealed class FakeBrokerRuntimeRegistry : IBrokerRuntimeRegistry
    {
        private readonly Dictionary<string, BrokerRuntimeSnapshot> _snapshots = new(StringComparer.Ordinal);
        private readonly Dictionary<string, RuntimePipelineSnapshot> _pipelineSnapshots = new(StringComparer.Ordinal);

        public BrokerRuntimeSnapshot GetSnapshot(string workspaceId)
        {
            return _snapshots.TryGetValue(workspaceId, out var snapshot)
                ? Clone(snapshot)
                : new BrokerRuntimeSnapshot();
        }

        public void UpdateSnapshot(string workspaceId, BrokerRuntimeSnapshot snapshot)
        {
            _snapshots[workspaceId] = Clone(snapshot);
        }

        public RuntimePipelineSnapshot GetPipelineSnapshot(string workspaceId)
        {
            return _pipelineSnapshots.TryGetValue(workspaceId, out var snapshot)
                ? snapshot
                : new RuntimePipelineSnapshot();
        }

        public void UpdatePipelineSnapshot(string workspaceId, RuntimePipelineSnapshot snapshot)
        {
            _pipelineSnapshots[workspaceId] = snapshot;
        }

        private static BrokerRuntimeSnapshot Clone(BrokerRuntimeSnapshot snapshot)
        {
            return new BrokerRuntimeSnapshot
            {
                ActiveServerProfileId = snapshot.ActiveServerProfileId,
                ActiveServerName = snapshot.ActiveServerName,
                ActiveServerAddress = snapshot.ActiveServerAddress,
                IsConnected = snapshot.IsConnected,
                LastStatusMessage = snapshot.LastStatusMessage,
                TopicFilters = [..snapshot.TopicFilters]
            };
        }
    }
}
