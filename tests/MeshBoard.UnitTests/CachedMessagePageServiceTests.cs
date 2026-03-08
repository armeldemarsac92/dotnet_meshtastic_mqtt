using MeshBoard.Application.Abstractions.Workspaces;
using MeshBoard.Application.Caching;
using MeshBoard.Application.Observability;
using MeshBoard.Application.Services;
using MeshBoard.Contracts.Diagnostics;
using MeshBoard.Contracts.Messages;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeshBoard.UnitTests;

public sealed class CachedMessagePageServiceTests
{
    [Fact]
    public async Task GetMessagesPage_ShouldReuseCachedResult_UntilInvalidated()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 128 });
        var innerService = new FakeMessageService();
        var invalidator = new InMemoryReadModelCacheInvalidator();
        var metricsService = new InMemoryReadModelMetricsService();
        var service = new CachedMessagePageService(
            innerService,
            memoryCache,
            new FakeWorkspaceContextAccessor(),
            invalidator,
            metricsService,
            NullLogger<CachedMessagePageService>.Instance);
        var query = CreateQuery();

        var firstPage = await service.GetMessagesPage(query, offset: 0, take: 50);
        var secondPage = await service.GetMessagesPage(query, offset: 0, take: 50);

        Assert.Equal(1, innerService.GetMessagesPageCallCount);
        Assert.Same(firstPage, secondPage);

        invalidator.Invalidate("workspace-tests", ReadModelCacheRegion.MessagePages);

        var thirdPage = await service.GetMessagesPage(query, offset: 0, take: 50);

        Assert.Equal(2, innerService.GetMessagesPageCallCount);
        Assert.NotSame(firstPage, thirdPage);

        var snapshot = metricsService.GetSnapshots().Single(metric => metric.Kind == ReadModelMetricKind.MessagePage);
        Assert.Equal(1, snapshot.CacheHitCount);
        Assert.Equal(2, snapshot.CacheMissCount);
        Assert.Equal(2, snapshot.LoadCount);
    }

    [Fact]
    public async Task GetMessagesPage_ShouldBypassCache_WhenForceRefreshIsRequested()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 128 });
        var innerService = new FakeMessageService();
        var metricsService = new InMemoryReadModelMetricsService();
        var service = new CachedMessagePageService(
            innerService,
            memoryCache,
            new FakeWorkspaceContextAccessor(),
            new InMemoryReadModelCacheInvalidator(),
            metricsService,
            NullLogger<CachedMessagePageService>.Instance);
        var query = CreateQuery();

        await service.GetMessagesPage(query, offset: 0, take: 50);
        await service.GetMessagesPage(query, offset: 0, take: 50, forceRefresh: true);

        Assert.Equal(2, innerService.GetMessagesPageCallCount);

        var snapshot = metricsService.GetSnapshots().Single(metric => metric.Kind == ReadModelMetricKind.MessagePage);
        Assert.Equal(1, snapshot.ForcedRefreshCount);
    }

    private static MessageQuery CreateQuery()
    {
        return new MessageQuery
        {
            BrokerServer = "mqtt.meshtastic.org:1883",
            SearchText = "alpha",
            PacketType = "Text Message",
            Visibility = MessageVisibilityFilter.DecodedOnly
        };
    }

    private sealed class FakeWorkspaceContextAccessor : IWorkspaceContextAccessor
    {
        public string GetWorkspaceId()
        {
            return "workspace-tests";
        }
    }

    private sealed class FakeMessageService : IMessageService
    {
        public int GetMessagesPageCallCount { get; private set; }

        public Task<MessagePageResult> GetMessagesPage(
            MessageQuery? query = null,
            int offset = 0,
            int take = 100,
            CancellationToken cancellationToken = default)
        {
            GetMessagesPageCallCount++;

            return Task.FromResult(new MessagePageResult
            {
                TotalCount = 1,
                Items =
                [
                    new MessageSummary
                    {
                        Id = Guid.NewGuid(),
                        BrokerServer = query?.BrokerServer ?? string.Empty,
                        Topic = "msh/US/2/e/LongFast/!abc12345",
                        PacketType = query?.PacketType ?? "Text Message",
                        FromNodeId = "!abc12345",
                        PayloadPreview = $"call-{GetMessagesPageCallCount}",
                        ReceivedAtUtc = DateTimeOffset.UtcNow
                    }
                ]
            });
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
