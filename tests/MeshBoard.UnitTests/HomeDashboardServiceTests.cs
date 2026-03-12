using MeshBoard.Application.Abstractions.Workspaces;
using MeshBoard.Application.Caching;
using MeshBoard.Application.Observability;
using MeshBoard.Application.Services;
using MeshBoard.Contracts.Diagnostics;
using MeshBoard.Contracts.Favorites;
using MeshBoard.Contracts.Messages;
using MeshBoard.Contracts.Meshtastic;
using MeshBoard.Contracts.Nodes;
using MeshBoard.Contracts.Topics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeshBoard.UnitTests;

public sealed class HomeDashboardServiceTests
{
    [Fact]
    public async Task GetSnapshot_ShouldReuseCachedSnapshot_UntilInvalidated()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 128 });
        var brokerMonitorService = new FakeBrokerMonitorService();
        var favoriteNodeService = new FakeFavoriteNodeService();
        var messageService = new FakeMessageService();
        var nodeService = new FakeNodeService();
        var topicPresetService = new FakeTopicPresetService();
        var invalidator = new InMemoryReadModelCacheInvalidator();
        var metricsService = new InMemoryReadModelMetricsService();
        var service = new HomeDashboardService(
            brokerMonitorService,
            favoriteNodeService,
            memoryCache,
            messageService,
            nodeService,
            topicPresetService,
            new FakeWorkspaceContextAccessor(),
            invalidator,
            metricsService,
            NullLogger<HomeDashboardService>.Instance);

        var firstSnapshot = await service.GetSnapshot();
        var secondSnapshot = await service.GetSnapshot();

        Assert.Equal(1, brokerMonitorService.GetBrokerStatusCallCount);
        Assert.Equal(1, favoriteNodeService.GetFavoriteNodesCallCount);
        Assert.Equal(1, messageService.GetRecentMessagesCallCount);
        Assert.Equal(1, nodeService.CountNodesCallCount);
        Assert.Equal(1, topicPresetService.GetTopicPresetsCallCount);
        Assert.Same(firstSnapshot, secondSnapshot);

        invalidator.Invalidate("workspace-tests", ReadModelCacheRegion.Dashboard);

        var thirdSnapshot = await service.GetSnapshot();

        Assert.Equal(2, brokerMonitorService.GetBrokerStatusCallCount);
        Assert.Equal(2, favoriteNodeService.GetFavoriteNodesCallCount);
        Assert.Equal(2, messageService.GetRecentMessagesCallCount);
        Assert.Equal(2, nodeService.CountNodesCallCount);
        Assert.Equal(2, topicPresetService.GetTopicPresetsCallCount);
        Assert.NotSame(firstSnapshot, thirdSnapshot);

        var snapshot = metricsService.GetSnapshots().Single(metric => metric.Kind == ReadModelMetricKind.DashboardSnapshot);
        Assert.Equal(1, snapshot.CacheHitCount);
        Assert.Equal(2, snapshot.CacheMissCount);
        Assert.Equal(2, snapshot.LoadCount);
    }

    [Fact]
    public async Task GetSnapshot_ShouldBypassCache_WhenForceRefreshIsRequested()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 128 });
        var brokerMonitorService = new FakeBrokerMonitorService();
        var favoriteNodeService = new FakeFavoriteNodeService();
        var messageService = new FakeMessageService();
        var nodeService = new FakeNodeService();
        var topicPresetService = new FakeTopicPresetService();
        var metricsService = new InMemoryReadModelMetricsService();
        var service = new HomeDashboardService(
            brokerMonitorService,
            favoriteNodeService,
            memoryCache,
            messageService,
            nodeService,
            topicPresetService,
            new FakeWorkspaceContextAccessor(),
            new InMemoryReadModelCacheInvalidator(),
            metricsService,
            NullLogger<HomeDashboardService>.Instance);

        await service.GetSnapshot();
        await service.GetSnapshot(forceRefresh: true);

        Assert.Equal(2, brokerMonitorService.GetBrokerStatusCallCount);
        Assert.Equal(2, favoriteNodeService.GetFavoriteNodesCallCount);
        Assert.Equal(2, messageService.GetRecentMessagesCallCount);
        Assert.Equal(2, nodeService.CountNodesCallCount);
        Assert.Equal(2, topicPresetService.GetTopicPresetsCallCount);

        var snapshot = metricsService.GetSnapshots().Single(metric => metric.Kind == ReadModelMetricKind.DashboardSnapshot);
        Assert.Equal(1, snapshot.ForcedRefreshCount);
    }

    private sealed class FakeWorkspaceContextAccessor : IWorkspaceContextAccessor
    {
        public string GetWorkspaceId()
        {
            return "workspace-tests";
        }
    }

    private sealed class FakeBrokerMonitorService : IBrokerMonitorService
    {
        public int GetBrokerStatusCallCount { get; private set; }

        public Task EnsureConnected(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public BrokerStatus GetBrokerStatus()
        {
            GetBrokerStatusCallCount++;

            return new BrokerStatus
            {
                ActiveServerName = $"Server {GetBrokerStatusCallCount}",
                ActiveServerAddress = "mqtt.meshtastic.org:1883",
                Host = "mqtt.meshtastic.org",
                Port = 1883,
                IsConnected = true
            };
        }

        public Task SubscribeToDefaultTopic(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task SubscribeToTopic(string topicFilter, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task SubscribeToEphemeralTopic(string topicFilter, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task SwitchActiveServerProfile(Guid profileId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task UnsubscribeFromTopic(string topicFilter, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task UnsubscribeFromEphemeralTopic(string topicFilter, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeFavoriteNodeService : IFavoriteNodeService
    {
        public int GetFavoriteNodesCallCount { get; private set; }

        public Task<FavoriteNode> SaveFavoriteNode(
            SaveFavoriteNodeRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyCollection<FavoriteNode>> GetFavoriteNodes(CancellationToken cancellationToken = default)
        {
            GetFavoriteNodesCallCount++;
            IReadOnlyCollection<FavoriteNode> favorites =
            [
                new FavoriteNode { NodeId = "!abc12345", ShortName = "ALPHA" }
            ];

            return Task.FromResult(favorites);
        }

        public Task RemoveFavoriteNode(string nodeId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeMessageService : IMessageService
    {
        public int GetRecentMessagesCallCount { get; private set; }

        public Task<MessagePageResult> GetMessagesPage(
            MessageQuery? query = null,
            int offset = 0,
            int take = 100,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<MessagePageResult> GetMessagesPageBySender(
            string senderNodeId,
            int offset = 0,
            int take = 100,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyCollection<MessageSummary>> GetRecentMessages(
            int take = 250,
            CancellationToken cancellationToken = default)
        {
            GetRecentMessagesCallCount++;
            IReadOnlyCollection<MessageSummary> messages =
            [
                new MessageSummary
                {
                    Id = Guid.NewGuid(),
                    BrokerServer = "mqtt.meshtastic.org:1883",
                    Topic = "msh/US/2/e/LongFast/!abc12345",
                    PacketType = "Text Message",
                    FromNodeId = "!abc12345",
                    PayloadPreview = "hello",
                    ReceivedAtUtc = DateTimeOffset.UtcNow
                }
            ];

            return Task.FromResult(messages);
        }

        public Task<IReadOnlyCollection<MessageSummary>> GetRecentMessagesByBroker(
            string brokerServer,
            int take = 250,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyCollection<MessageSummary>> GetRecentMessagesByChannel(
            string region,
            string channel,
            int take = 250,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyCollection<MessageSummary>> GetRecentMessagesBySender(
            string senderNodeId,
            int take = 250,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeNodeService : INodeService
    {
        public int CountNodesCallCount { get; private set; }

        public Task<int> CountNodes(NodeQuery? query = null, CancellationToken cancellationToken = default)
        {
            CountNodesCallCount++;
            return Task.FromResult(7);
        }

        public Task<NodeSummary?> GetNodeById(string nodeId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyCollection<NodeSummary>> GetNodes(
            NodeQuery? query = null,
            int take = 100,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyCollection<NodeSummary>> GetLocatedNodes(
            string? searchText = null,
            int take = 5000,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<NodePageResult> GetNodesPage(
            NodeQuery? query = null,
            int offset = 0,
            int take = 100,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeTopicPresetService : ITopicPresetService
    {
        public int GetTopicPresetsCallCount { get; private set; }

        public Task<TopicPreset> SaveTopicPreset(
            SaveTopicPresetRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<TopicPreset?> GetTopicPresetByPattern(string topicPattern, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<TopicPreset?>(null);
        }

        public Task<IReadOnlyCollection<TopicPreset>> GetTopicPresets(CancellationToken cancellationToken = default)
        {
            GetTopicPresetsCallCount++;
            IReadOnlyCollection<TopicPreset> presets =
            [
                new TopicPreset
                {
                    Id = Guid.NewGuid(),
                    Name = "LongFast",
                    TopicPattern = "msh/US/2/e/LongFast/#"
                }
            ];

            return Task.FromResult(presets);
        }
    }
}
