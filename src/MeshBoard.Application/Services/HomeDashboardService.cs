using System.Diagnostics;
using MeshBoard.Application.Caching;
using MeshBoard.Application.Abstractions.Workspaces;
using MeshBoard.Application.Observability;
using MeshBoard.Contracts.Dashboard;
using MeshBoard.Contracts.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Application.Services;

public interface IHomeDashboardService
{
    Task<HomeDashboardSnapshot> GetSnapshot(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default);
}

public sealed class HomeDashboardService : IHomeDashboardService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(15);

    private readonly IBrokerMonitorService _brokerMonitorService;
    private readonly IFavoriteNodeService _favoriteNodeService;
    private readonly IReadModelCacheInvalidator _readModelCacheInvalidator;
    private readonly IReadModelMetricsService _readModelMetricsService;
    private readonly ILogger<HomeDashboardService> _logger;
    private readonly IMemoryCache _memoryCache;
    private readonly IMessageService _messageService;
    private readonly INodeService _nodeService;
    private readonly ITopicPresetService _topicPresetService;
    private readonly IWorkspaceContextAccessor _workspaceContextAccessor;

    public HomeDashboardService(
        IBrokerMonitorService brokerMonitorService,
        IFavoriteNodeService favoriteNodeService,
        IMemoryCache memoryCache,
        IMessageService messageService,
        INodeService nodeService,
        ITopicPresetService topicPresetService,
        IWorkspaceContextAccessor workspaceContextAccessor,
        IReadModelCacheInvalidator readModelCacheInvalidator,
        IReadModelMetricsService readModelMetricsService,
        ILogger<HomeDashboardService> logger)
    {
        _brokerMonitorService = brokerMonitorService;
        _favoriteNodeService = favoriteNodeService;
        _memoryCache = memoryCache;
        _messageService = messageService;
        _nodeService = nodeService;
        _topicPresetService = topicPresetService;
        _workspaceContextAccessor = workspaceContextAccessor;
        _readModelCacheInvalidator = readModelCacheInvalidator;
        _readModelMetricsService = readModelMetricsService;
        _logger = logger;
    }

    public async Task<HomeDashboardSnapshot> GetSnapshot(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        var workspaceId = _workspaceContextAccessor.GetWorkspaceId();
        var cacheKey = CreateCacheKey(workspaceId);

        if (!forceRefresh &&
            _memoryCache.TryGetValue<HomeDashboardSnapshot>(cacheKey, out var cachedSnapshot) &&
            cachedSnapshot is not null)
        {
            _readModelMetricsService.RecordCacheHit(ReadModelMetricKind.DashboardSnapshot);
            _logger.LogDebug("Returning cached dashboard snapshot for workspace {WorkspaceId}", workspaceId);
            return cachedSnapshot;
        }

        _readModelMetricsService.RecordCacheMiss(ReadModelMetricKind.DashboardSnapshot, forceRefresh);
        _logger.LogDebug("Loading dashboard snapshot for workspace {WorkspaceId}", workspaceId);
        var startedAt = Stopwatch.GetTimestamp();

        var brokerStatus = _brokerMonitorService.GetBrokerStatus();
        var topicPresetsTask = _topicPresetService.GetTopicPresets(cancellationToken);
        var favoriteNodesTask = _favoriteNodeService.GetFavoriteNodes(cancellationToken);
        var observedNodeCountTask = _nodeService.GetNodesPage(offset: 0, take: 1, cancellationToken: cancellationToken);
        var messagesTask = _messageService.GetRecentMessages(120, cancellationToken);

        await Task.WhenAll(topicPresetsTask, favoriteNodesTask, observedNodeCountTask, messagesTask);

        var snapshot = new HomeDashboardSnapshot
        {
            BrokerStatus = brokerStatus,
            TopicPresets = await topicPresetsTask,
            FavoriteNodes = await favoriteNodesTask,
            ObservedNodeCount = (await observedNodeCountTask).TotalCount,
            Messages = await messagesTask
        };

        _memoryCache.Set(
            cacheKey,
            snapshot,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheDuration,
                Size = 1
            });

        _readModelMetricsService.RecordLoadDuration(
            ReadModelMetricKind.DashboardSnapshot,
            Stopwatch.GetElapsedTime(startedAt));

        return snapshot;
    }

    private string CreateCacheKey(string workspaceId)
    {
        var stamp = _readModelCacheInvalidator.GetStamp(workspaceId, ReadModelCacheRegion.Dashboard);
        return $"dashboard::{workspaceId}::{stamp}";
    }
}
