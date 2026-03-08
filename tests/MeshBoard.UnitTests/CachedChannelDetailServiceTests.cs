using MeshBoard.Application.Abstractions.Workspaces;
using MeshBoard.Application.Caching;
using MeshBoard.Application.Observability;
using MeshBoard.Application.Services;
using MeshBoard.Contracts.Diagnostics;
using MeshBoard.Contracts.Messages;
using MeshBoard.Contracts.Topics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeshBoard.UnitTests;

public sealed class CachedChannelDetailServiceTests
{
    [Fact]
    public async Task ChannelReads_ShouldReuseCachedResults_UntilInvalidated()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 128 });
        var invalidator = new InMemoryReadModelCacheInvalidator();
        var metricsService = new InMemoryReadModelMetricsService();
        var innerService = new FakeChannelReadService();
        var service = new CachedChannelDetailService(
            innerService,
            memoryCache,
            new FakeWorkspaceContextAccessor(),
            invalidator,
            metricsService,
            NullLogger<CachedChannelDetailService>.Instance);

        var firstSummary = await service.GetChannelSummary("US", "LongFast");
        var secondSummary = await service.GetChannelSummary("US", "LongFast");
        var firstTopNodes = await service.GetTopNodesByChannel("US", "LongFast", 12);
        var secondTopNodes = await service.GetTopNodesByChannel("US", "LongFast", 12);
        var firstPage = await service.GetMessagesPageByChannel("US", "LongFast", offset: 0, take: 25);
        var secondPage = await service.GetMessagesPageByChannel("US", "LongFast", offset: 0, take: 25);

        Assert.Equal(1, innerService.GetChannelSummaryCallCount);
        Assert.Equal(1, innerService.GetTopNodesCallCount);
        Assert.Equal(1, innerService.GetMessagesPageCallCount);
        Assert.Same(firstSummary, secondSummary);
        Assert.Same(firstTopNodes, secondTopNodes);
        Assert.Same(firstPage, secondPage);

        invalidator.Invalidate("workspace-tests", ReadModelCacheRegion.ChannelDetails);

        var thirdSummary = await service.GetChannelSummary("US", "LongFast");
        var thirdTopNodes = await service.GetTopNodesByChannel("US", "LongFast", 12);
        var thirdPage = await service.GetMessagesPageByChannel("US", "LongFast", offset: 0, take: 25);

        Assert.Equal(2, innerService.GetChannelSummaryCallCount);
        Assert.Equal(2, innerService.GetTopNodesCallCount);
        Assert.Equal(2, innerService.GetMessagesPageCallCount);
        Assert.NotSame(firstSummary, thirdSummary);
        Assert.NotSame(firstTopNodes, thirdTopNodes);
        Assert.NotSame(firstPage, thirdPage);

        var snapshots = metricsService.GetSnapshots().ToDictionary(metric => metric.Kind);
        Assert.Equal(1, snapshots[ReadModelMetricKind.ChannelSummary].CacheHitCount);
        Assert.Equal(2, snapshots[ReadModelMetricKind.ChannelSummary].CacheMissCount);
        Assert.Equal(1, snapshots[ReadModelMetricKind.ChannelTopNodes].CacheHitCount);
        Assert.Equal(2, snapshots[ReadModelMetricKind.ChannelTopNodes].CacheMissCount);
        Assert.Equal(1, snapshots[ReadModelMetricKind.ChannelMessagePage].CacheHitCount);
        Assert.Equal(2, snapshots[ReadModelMetricKind.ChannelMessagePage].CacheMissCount);
    }

    [Fact]
    public async Task GetChannelSummary_ShouldBypassCache_WhenForceRefreshIsRequested()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 128 });
        var innerService = new FakeChannelReadService();
        var metricsService = new InMemoryReadModelMetricsService();
        var service = new CachedChannelDetailService(
            innerService,
            memoryCache,
            new FakeWorkspaceContextAccessor(),
            new InMemoryReadModelCacheInvalidator(),
            metricsService,
            NullLogger<CachedChannelDetailService>.Instance);

        await service.GetChannelSummary("US", "LongFast");
        var summary = await service.GetChannelSummary("US", "LongFast", forceRefresh: true);

        Assert.Equal(2, innerService.GetChannelSummaryCallCount);
        Assert.Equal(20, summary.PacketCount);

        var snapshot = metricsService.GetSnapshots().Single(metric => metric.Kind == ReadModelMetricKind.ChannelSummary);
        Assert.Equal(1, snapshot.ForcedRefreshCount);
    }

    private sealed class FakeWorkspaceContextAccessor : IWorkspaceContextAccessor
    {
        public string GetWorkspaceId()
        {
            return "workspace-tests";
        }
    }

    private sealed class FakeChannelReadService : IChannelReadService
    {
        public int GetChannelSummaryCallCount { get; private set; }

        public int GetMessagesPageCallCount { get; private set; }

        public int GetTopNodesCallCount { get; private set; }

        public Task<MessagePageResult> GetMessagesPageByChannel(
            string region,
            string channel,
            int offset = 0,
            int take = 25,
            CancellationToken cancellationToken = default)
        {
            GetMessagesPageCallCount++;

            return Task.FromResult(new MessagePageResult
            {
                TotalCount = 3,
                Items =
                [
                    new MessageSummary
                    {
                        Id = Guid.NewGuid(),
                        FromNodeId = "!abc12345",
                        Topic = $"msh/{region}/2/e/{channel}/!abc12345",
                        PacketType = "Text Message",
                        PayloadPreview = $"page-{GetMessagesPageCallCount}",
                        ReceivedAtUtc = DateTimeOffset.UtcNow
                    }
                ]
            });
        }

        public Task<ChannelSummary> GetChannelSummary(
            string region,
            string channel,
            CancellationToken cancellationToken = default)
        {
            GetChannelSummaryCallCount++;

            return Task.FromResult(new ChannelSummary
            {
                PacketCount = GetChannelSummaryCallCount * 10,
                UniqueSenderCount = 4,
                DecodedPacketCount = 8,
                ObservedBrokerServers = ["mqtt.meshtastic.org:1883"]
            });
        }

        public Task<IReadOnlyCollection<ChannelTopNode>> GetTopNodesByChannel(
            string region,
            string channel,
            int take = 12,
            CancellationToken cancellationToken = default)
        {
            GetTopNodesCallCount++;

            IReadOnlyCollection<ChannelTopNode> topNodes =
            [
                new ChannelTopNode
                {
                    NodeId = "!abc12345",
                    DisplayName = $"NODE-{GetTopNodesCallCount}",
                    PacketCount = 5
                }
            ];

            return Task.FromResult(topNodes);
        }
    }
}
