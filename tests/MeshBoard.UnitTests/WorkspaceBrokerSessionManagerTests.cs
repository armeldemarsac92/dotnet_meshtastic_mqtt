using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Meshtastic;
using MeshBoard.Contracts.Workspaces;
using MeshBoard.Infrastructure.Meshtastic.Mqtt;
using MeshBoard.Infrastructure.Meshtastic.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeshBoard.UnitTests;

public sealed class SingleMqttSessionWorkspaceBrokerSessionManagerTests
{
    [Fact]
    public async Task ConnectAsync_ShouldCreateIndependentSessions_PerWorkspace()
    {
        var sessionFactory = new FakeMqttSessionFactory();
        var repository = new FakeBrokerServerProfileRepository(
            CreateProfile("workspace-a", "mqtt-a.example.org"),
            CreateProfile("workspace-b", "mqtt-b.example.org"));
        var manager = new WorkspaceBrokerSessionManager(
            CreateScopeFactory(repository),
            sessionFactory,
            NullLogger<WorkspaceBrokerSessionManager>.Instance);

        await manager.ConnectAsync("workspace-a");
        await manager.ConnectAsync("workspace-b");

        Assert.True(manager.IsConnected("workspace-a"));
        Assert.True(manager.IsConnected("workspace-b"));
        Assert.Equal(2, sessionFactory.CreatedSettings.Count);
        Assert.Contains(sessionFactory.CreatedSettings, settings => settings.WorkspaceId == "workspace-a" && settings.Host == "mqtt-a.example.org");
        Assert.Contains(sessionFactory.CreatedSettings, settings => settings.WorkspaceId == "workspace-b" && settings.Host == "mqtt-b.example.org");
    }

    [Fact]
    public async Task ResetRuntimeAsync_ShouldReplaceSession_WhenActiveProfileChanges()
    {
        var initialProfile = CreateProfile("workspace-a", "mqtt-a.example.org");
        var updatedProfile = CreateProfile("workspace-a", "mqtt-new.example.org");
        var sessionFactory = new FakeMqttSessionFactory();
        var repository = new FakeBrokerServerProfileRepository(initialProfile);
        var manager = new WorkspaceBrokerSessionManager(
            CreateScopeFactory(repository),
            sessionFactory,
            NullLogger<WorkspaceBrokerSessionManager>.Instance);

        await manager.ConnectAsync("workspace-a");
        repository.SetActiveProfile(updatedProfile);
        await manager.ResetRuntimeAsync("workspace-a");
        await manager.ConnectAsync("workspace-a");

        Assert.Equal(2, sessionFactory.CreatedSettings.Count);
        Assert.Equal("mqtt-a.example.org", sessionFactory.CreatedSettings[0].Host);
        Assert.Equal("mqtt-new.example.org", sessionFactory.CreatedSettings[1].Host);
    }

    [Fact]
    public async Task MessageReceived_ShouldFlowWorkspaceIdentity_FromSessionRuntime()
    {
        var profile = CreateProfile("workspace-a", "mqtt-a.example.org");
        var sessionFactory = new FakeMqttSessionFactory();
        var manager = new WorkspaceBrokerSessionManager(
            CreateScopeFactory(new FakeBrokerServerProfileRepository(profile)),
            sessionFactory,
            NullLogger<WorkspaceBrokerSessionManager>.Instance);
        MqttInboundMessage? observedMessage = null;
        manager.MessageReceived += message =>
        {
            observedMessage = message;
            return Task.CompletedTask;
        };

        await manager.ConnectAsync("workspace-a");
        sessionFactory.LastCreatedSession!.RaiseMessageReceived(new MqttInboundMessage
        {
            WorkspaceId = "workspace-a",
            BrokerServer = "mqtt-a.example.org:1883",
            Topic = "msh/US/2/e/#",
            Payload = [],
            ReceivedAtUtc = DateTimeOffset.UtcNow
        });

        Assert.NotNull(observedMessage);
        Assert.Equal("workspace-a", observedMessage!.WorkspaceId);
    }

    private static BrokerServerProfile CreateProfile(string workspaceId, string host)
    {
        return new BrokerServerProfile
        {
            Id = Guid.NewGuid(),
            Name = $"Profile-{workspaceId}",
            Host = host,
            Port = 1883,
            UseTls = false,
            Username = string.Empty,
            Password = string.Empty,
            DefaultTopicPattern = "msh/US/2/e/#",
            DownlinkTopic = "msh/US/2/json/mqtt/",
            EnableSend = true,
            IsActive = true
        };
    }

    private static IServiceScopeFactory CreateScopeFactory(FakeBrokerServerProfileRepository repository)
    {
        var services = new ServiceCollection();
        services.AddScoped<IBrokerServerProfileRepository>(_ => repository);
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private sealed class FakeBrokerServerProfileRepository : IBrokerServerProfileRepository
    {
        private readonly Dictionary<string, BrokerServerProfile> _profilesByWorkspace;

        public FakeBrokerServerProfileRepository(params BrokerServerProfile[] profiles)
        {
            _profilesByWorkspace = profiles.ToDictionary(profile => profile.Name.Replace("Profile-", string.Empty), profile => profile, StringComparer.Ordinal);
        }

        public Task<IReadOnlyCollection<WorkspaceBrokerServerProfile>> GetAllActiveAsync(
            CancellationToken cancellationToken = default)
        {
            IReadOnlyCollection<WorkspaceBrokerServerProfile> profiles = _profilesByWorkspace
                .Select(entry => new WorkspaceBrokerServerProfile
                {
                    WorkspaceId = entry.Key,
                    Profile = entry.Value
                })
                .ToList();

            return Task.FromResult(profiles);
        }

        public Task<IReadOnlyCollection<BrokerServerProfile>> GetAllAsync(string workspaceId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<BrokerServerProfile>>(
                _profilesByWorkspace.TryGetValue(workspaceId, out var profile) ? [profile] : []);
        }

        public Task<BrokerServerProfile?> GetActiveAsync(string workspaceId, CancellationToken cancellationToken = default)
        {
            _profilesByWorkspace.TryGetValue(workspaceId, out var profile);
            return Task.FromResult(profile);
        }

        public Task<BrokerServerProfile?> GetByIdAsync(string workspaceId, Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_profilesByWorkspace.TryGetValue(workspaceId, out var profile) && profile.Id == id ? profile : null);
        }

        public Task SetExclusiveActiveAsync(string workspaceId, Guid id, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<bool> AreSubscriptionIntentsInitializedAsync(string workspaceId, Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        public Task MarkSubscriptionIntentsInitializedAsync(string workspaceId, Guid id, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<BrokerServerProfile> UpsertAsync(string workspaceId, SaveBrokerServerProfileRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public void SetActiveProfile(BrokerServerProfile profile)
        {
            _profilesByWorkspace[profile.Name.Replace("Profile-", string.Empty)] = profile;
        }
    }

#pragma warning disable CS0067
    private sealed class FakeMqttSessionFactory : IMqttSessionFactory
    {
        public List<MqttSessionConnectionSettings> CreatedSettings { get; } = [];

        public FakeMqttSession? LastCreatedSession { get; private set; }

        public IMqttSession Create(MqttSessionConnectionSettings settings)
        {
            CreatedSettings.Add(settings);
            LastCreatedSession = new FakeMqttSession(settings);
            return LastCreatedSession;
        }
    }

    private sealed class FakeMqttSession : IMqttSession
    {
        private readonly MqttSessionConnectionSettings _settings;

        public FakeMqttSession(MqttSessionConnectionSettings settings)
        {
            _settings = settings;
        }

        public bool IsConnected { get; private set; }

        public string? LastStatusMessage => null;

        public IReadOnlyCollection<string> TopicFilters => [];

        public event Func<bool, Task>? ConnectionStateChanged;

        public event Func<MqttInboundMessage, Task>? MessageReceived;

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
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
            return Task.CompletedTask;
        }

        public Task UnsubscribeAsync(string topicFilter, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public void RaiseMessageReceived(MqttInboundMessage inboundMessage)
        {
            if (string.IsNullOrWhiteSpace(inboundMessage.WorkspaceId))
            {
                inboundMessage.WorkspaceId = _settings.WorkspaceId;
            }

            MessageReceived?.Invoke(inboundMessage).GetAwaiter().GetResult();
        }
    }
#pragma warning restore CS0067
}
