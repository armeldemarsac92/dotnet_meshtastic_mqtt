using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Application.Abstractions.Workspaces;
using MeshBoard.Application.Services;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Topics;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeshBoard.UnitTests;

public sealed class TopicDiscoveryServiceTests
{
    [Fact]
    public async Task RecordObservedTopic_ShouldNormalizeAndPersistTopicPattern()
    {
        var repository = new FakeDiscoveredTopicRepository();
        var service = new TopicDiscoveryService(
            new FakeBrokerServerProfileService(),
            repository,
            new TopicExplorerService(),
            new FakeWorkspaceContextAccessor(),
            NullLogger<TopicDiscoveryService>.Instance);
        var observedAtUtc = new DateTimeOffset(2026, 3, 4, 23, 0, 0, TimeSpan.Zero);

        await service.RecordObservedTopic("msh/EU_868/2/json/MediumFast/!12345678", observedAtUtc, "mqtt.eu:1883");

        Assert.Equal("workspace-tests", repository.LastWorkspaceId);
        Assert.Equal("mqtt.eu:1883", repository.LastBrokerServer);
        Assert.Equal("msh/EU_868/2/e/MediumFast/#", repository.LastTopicPattern);
        Assert.Equal("EU_868", repository.LastRegion);
        Assert.Equal("MediumFast", repository.LastChannel);
        Assert.Equal(observedAtUtc, repository.LastObservedAtUtc);
    }

    [Fact]
    public async Task RecordObservedTopic_ShouldIgnoreNonMeshtasticTopic()
    {
        var repository = new FakeDiscoveredTopicRepository();
        var service = new TopicDiscoveryService(
            new FakeBrokerServerProfileService(),
            repository,
            new TopicExplorerService(),
            new FakeWorkspaceContextAccessor(),
            NullLogger<TopicDiscoveryService>.Instance);

        await service.RecordObservedTopic("invalid/topic/value", DateTimeOffset.UtcNow);

        Assert.Equal(0, repository.UpsertCount);
    }

    [Fact]
    public async Task RecordObservedTopic_ShouldUseExplicitWorkspaceIdWhenProvided()
    {
        var repository = new FakeDiscoveredTopicRepository();
        var service = new TopicDiscoveryService(
            new FakeBrokerServerProfileService(),
            repository,
            new TopicExplorerService(),
            new FakeWorkspaceContextAccessor(),
            NullLogger<TopicDiscoveryService>.Instance);

        await service.RecordObservedTopic(
            "msh/EU_868/2/json/MediumFast/!12345678",
            DateTimeOffset.UtcNow,
            "mqtt.eu:1883",
            "workspace-explicit");

        Assert.Equal("workspace-explicit", repository.LastWorkspaceId);
    }

    private sealed class FakeDiscoveredTopicRepository : IDiscoveredTopicRepository
    {
        public string? LastWorkspaceId { get; private set; }

        public string? LastBrokerServer { get; private set; }

        public string? LastChannel { get; private set; }

        public DateTimeOffset? LastObservedAtUtc { get; private set; }

        public string? LastRegion { get; private set; }

        public string? LastTopicPattern { get; private set; }

        public int UpsertCount { get; private set; }

        public Task<IReadOnlyCollection<TopicCatalogEntry>> GetAllAsync(
            string workspaceId,
            string brokerServer,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyCollection<TopicCatalogEntry> discoveredTopics = [];
            return Task.FromResult(discoveredTopics);
        }

        public Task UpsertAsync(
            string workspaceId,
            string brokerServer,
            string topicPattern,
            string region,
            string channel,
            DateTimeOffset observedAtUtc,
            CancellationToken cancellationToken = default)
        {
            LastWorkspaceId = workspaceId;
            LastBrokerServer = brokerServer;
            LastTopicPattern = topicPattern;
            LastRegion = region;
            LastChannel = channel;
            LastObservedAtUtc = observedAtUtc;
            UpsertCount++;
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

    private sealed class FakeBrokerServerProfileService : IBrokerServerProfileService
    {
        public Task<IReadOnlyCollection<BrokerServerProfile>> GetServerProfiles(CancellationToken cancellationToken = default)
        {
            IReadOnlyCollection<BrokerServerProfile> profiles = [];
            return Task.FromResult(profiles);
        }

        public Task<BrokerServerProfile> GetActiveServerProfile(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new BrokerServerProfile
                {
                    Id = Guid.NewGuid(),
                    Name = "Default",
                    Host = "mqtt.meshtastic.org",
                    Port = 1883,
                    DefaultTopicPattern = "msh/US/2/e/#",
                    DownlinkTopic = "msh/US/2/json/mqtt/"
                });
        }

        public Task<BrokerServerProfile?> GetServerProfileById(Guid profileId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<BrokerServerProfile?>(null);
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
