using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Infrastructure.Meshtastic.Runtime;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeshBoard.UnitTests;

public sealed class BrokerRuntimeBootstrapServiceTests
{
    [Fact]
    public async Task InitializeActiveWorkspacesAsync_ShouldReconcileAllActiveWorkspaces()
    {
        var workspaceAProfile = CreateProfile(
            "workspace-a",
            "Workspace A",
            "mqtt-a.example.org",
            "msh/US/2/e/LongFast/#");
        var workspaceBProfile = CreateProfile(
            "workspace-b",
            "Workspace B",
            "mqtt-b.example.org",
            "msh/EU_868/2/e/MediumFast/#");
        var profileRepository = new FakeBrokerServerProfileRepository(workspaceAProfile, workspaceBProfile);
        var runtimeCommandService = new FakeBrokerRuntimeCommandService();
        var service = new BrokerRuntimeBootstrapService(
            profileRepository,
            runtimeCommandService,
            NullLogger<BrokerRuntimeBootstrapService>.Instance);

        await service.InitializeActiveWorkspacesAsync();

        Assert.Equal(
            [
                "workspace-a",
                "workspace-b"
            ],
            runtimeCommandService.ReconcileActiveProfileCalls.OrderBy(workspaceId => workspaceId, StringComparer.Ordinal).ToArray());
    }

    private static WorkspaceBrokerServerProfile CreateProfile(
        string workspaceId,
        string name,
        string host,
        string defaultTopicPattern)
    {
        return new WorkspaceBrokerServerProfile
        {
            WorkspaceId = workspaceId,
            Profile = new BrokerServerProfile
            {
                Id = Guid.NewGuid(),
                Name = name,
                Host = host,
                Port = 1883,
                UseTls = false,
                Username = string.Empty,
                Password = string.Empty,
                DefaultTopicPattern = defaultTopicPattern,
                DownlinkTopic = "msh/US/2/json/mqtt/",
                EnableSend = true,
                IsActive = true
            }
        };
    }

    private sealed class FakeBrokerServerProfileRepository : IBrokerServerProfileRepository
    {
        private readonly Dictionary<string, WorkspaceBrokerServerProfile> _activeProfilesByWorkspace;
        private readonly HashSet<string> _initializedProfiles = new(StringComparer.Ordinal);

        public FakeBrokerServerProfileRepository(params WorkspaceBrokerServerProfile[] profiles)
        {
            _activeProfilesByWorkspace = profiles.ToDictionary(profile => profile.WorkspaceId, StringComparer.Ordinal);
        }

        public Task<IReadOnlyCollection<WorkspaceBrokerServerProfile>> GetAllActiveAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<WorkspaceBrokerServerProfile>>(_activeProfilesByWorkspace.Values.ToList());
        }

        public Task<IReadOnlyCollection<BrokerServerProfile>> GetAllAsync(string workspaceId, CancellationToken cancellationToken = default)
        {
            IReadOnlyCollection<BrokerServerProfile> profiles = _activeProfilesByWorkspace.TryGetValue(workspaceId, out var profile)
                ? [profile.Profile]
                : [];
            return Task.FromResult(profiles);
        }

        public Task<BrokerServerProfile?> GetActiveAsync(string workspaceId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_activeProfilesByWorkspace.TryGetValue(workspaceId, out var profile)
                ? profile.Profile
                : null);
        }

        public Task<BrokerServerProfile?> GetByIdAsync(string workspaceId, Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_activeProfilesByWorkspace.TryGetValue(workspaceId, out var profile) && profile.Profile.Id == id
                ? profile.Profile
                : null);
        }

        public Task SetExclusiveActiveAsync(string workspaceId, Guid id, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<bool> AreSubscriptionIntentsInitializedAsync(string workspaceId, Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_initializedProfiles.Contains(BuildKey(workspaceId, id)));
        }

        public Task MarkSubscriptionIntentsInitializedAsync(string workspaceId, Guid id, CancellationToken cancellationToken = default)
        {
            _initializedProfiles.Add(BuildKey(workspaceId, id));
            return Task.CompletedTask;
        }

        public Task<BrokerServerProfile> UpsertAsync(string workspaceId, SaveBrokerServerProfileRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public void MarkInitialized(string workspaceId, Guid profileId)
        {
            _initializedProfiles.Add(BuildKey(workspaceId, profileId));
        }

        private static string BuildKey(string workspaceId, Guid profileId)
        {
            return $"{workspaceId}:{profileId}";
        }
    }
    private sealed class FakeBrokerRuntimeCommandService : IBrokerRuntimeCommandService
    {
        public List<string> EnsureConnectedCalls { get; } = [];

        public List<string> ReconcileActiveProfileCalls { get; } = [];

        public List<string> ResetAndReconnectActiveProfileCalls { get; } = [];

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
            return Task.CompletedTask;
        }

        public Task PublishAsync(string workspaceId, string topic, string payload, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SubscribeEphemeralAsync(string workspaceId, string topicFilter, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task UnsubscribeEphemeralAsync(string workspaceId, string topicFilter, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
