using MeshBoard.Application.Abstractions.Workspaces;
using MeshBoard.Application.Caching;
using MeshBoard.Application.Observability;
using MeshBoard.Application.Services;
using MeshBoard.Contracts.Diagnostics;
using MeshBoard.Contracts.Messages;
using MeshBoard.Contracts.Nodes;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeshBoard.UnitTests;

public sealed class CachedNodeDetailServiceTests
{
    [Fact]
    public async Task GetNodeById_AndSenderMessages_ShouldReuseCachedResults_UntilInvalidated()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 128 });
        var invalidator = new InMemoryReadModelCacheInvalidator();
        var metricsService = new InMemoryReadModelMetricsService();
        var messageService = new FakeMessageService();
        var nodeService = new FakeNodeService();
        var service = new CachedNodeDetailService(
            messageService,
            memoryCache,
            nodeService,
            new FakeWorkspaceContextAccessor(),
            invalidator,
            metricsService,
            NullLogger<CachedNodeDetailService>.Instance);

        var firstNode = await service.GetNodeById("!abc12345");
        var secondNode = await service.GetNodeById("!abc12345");
        var firstPage = await service.GetMessagesPageBySender("!abc12345", offset: 0, take: 25);
        var secondPage = await service.GetMessagesPageBySender("!abc12345", offset: 0, take: 25);

        Assert.Equal(1, nodeService.GetNodeByIdCallCount);
        Assert.Equal(1, messageService.GetMessagesPageBySenderCallCount);
        Assert.Same(firstNode, secondNode);
        Assert.Same(firstPage, secondPage);

        invalidator.Invalidate("workspace-tests", ReadModelCacheRegion.NodeDetails);

        var thirdNode = await service.GetNodeById("!abc12345");
        var thirdPage = await service.GetMessagesPageBySender("!abc12345", offset: 0, take: 25);

        Assert.Equal(2, nodeService.GetNodeByIdCallCount);
        Assert.Equal(2, messageService.GetMessagesPageBySenderCallCount);
        Assert.NotSame(firstNode, thirdNode);
        Assert.NotSame(firstPage, thirdPage);

        var nodeSnapshot = metricsService.GetSnapshots().Single(metric => metric.Kind == ReadModelMetricKind.NodeDetail);
        var messageSnapshot = metricsService.GetSnapshots().Single(metric => metric.Kind == ReadModelMetricKind.NodeMessagePage);
        Assert.Equal(1, nodeSnapshot.CacheHitCount);
        Assert.Equal(2, nodeSnapshot.CacheMissCount);
        Assert.Equal(1, messageSnapshot.CacheHitCount);
        Assert.Equal(2, messageSnapshot.CacheMissCount);
    }

    [Fact]
    public async Task GetMessagesPageBySender_ShouldBypassCache_WhenForceRefreshIsRequested()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 128 });
        var metricsService = new InMemoryReadModelMetricsService();
        var service = new CachedNodeDetailService(
            new FakeMessageService(),
            memoryCache,
            new FakeNodeService(),
            new FakeWorkspaceContextAccessor(),
            new InMemoryReadModelCacheInvalidator(),
            metricsService,
            NullLogger<CachedNodeDetailService>.Instance);

        await service.GetMessagesPageBySender("!abc12345", offset: 0, take: 25);
        var page = await service.GetMessagesPageBySender("!abc12345", offset: 0, take: 25, forceRefresh: true);

        Assert.Equal("refresh-2", page.Items.Single().PayloadPreview);

        var snapshot = metricsService.GetSnapshots().Single(metric => metric.Kind == ReadModelMetricKind.NodeMessagePage);
        Assert.Equal(1, snapshot.ForcedRefreshCount);
    }

    private sealed class FakeWorkspaceContextAccessor : IWorkspaceContextAccessor
    {
        public string GetWorkspaceId()
        {
            return "workspace-tests";
        }
    }

    private sealed class FakeNodeService : INodeService
    {
        public int GetNodeByIdCallCount { get; private set; }

        public Task<NodeSummary?> GetNodeById(string nodeId, CancellationToken cancellationToken = default)
        {
            GetNodeByIdCallCount++;

            return Task.FromResult<NodeSummary?>(new NodeSummary
            {
                NodeId = nodeId.Trim(),
                ShortName = $"NODE-{GetNodeByIdCallCount}"
            });
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

    private sealed class FakeMessageService : IMessageService
    {
        public int GetMessagesPageBySenderCallCount { get; private set; }

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
            GetMessagesPageBySenderCallCount++;

            return Task.FromResult(new MessagePageResult
            {
                TotalCount = 2,
                Items =
                [
                    new MessageSummary
                    {
                        Id = Guid.NewGuid(),
                        FromNodeId = senderNodeId.Trim(),
                        PayloadPreview = $"refresh-{GetMessagesPageBySenderCallCount}",
                        PacketType = "Text Message",
                        Topic = "msh/US/2/e/LongFast/!abc12345",
                        ReceivedAtUtc = DateTimeOffset.UtcNow
                    }
                ]
            });
        }

        public Task<IReadOnlyCollection<MessageSummary>> GetRecentMessages(
            int take = 250,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
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
}
