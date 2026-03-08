using MeshBoard.Application.Abstractions.Workspaces;
using MeshBoard.Application.Caching;
using MeshBoard.Contracts.Messages;
using MeshBoard.Contracts.Topics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Application.Services;

public interface ICachedChannelDetailService
{
    Task<MessagePageResult> GetMessagesPageByChannel(
        string region,
        string channel,
        int offset,
        int take,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default);

    Task<ChannelSummary> GetChannelSummary(
        string region,
        string channel,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ChannelTopNode>> GetTopNodesByChannel(
        string region,
        string channel,
        int take,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default);
}

public sealed class CachedChannelDetailService : ICachedChannelDetailService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(15);

    private readonly IChannelReadService _channelReadService;
    private readonly ILogger<CachedChannelDetailService> _logger;
    private readonly IMemoryCache _memoryCache;
    private readonly IReadModelCacheInvalidator _readModelCacheInvalidator;
    private readonly IWorkspaceContextAccessor _workspaceContextAccessor;

    public CachedChannelDetailService(
        IChannelReadService channelReadService,
        IMemoryCache memoryCache,
        IWorkspaceContextAccessor workspaceContextAccessor,
        IReadModelCacheInvalidator readModelCacheInvalidator,
        ILogger<CachedChannelDetailService> logger)
    {
        _channelReadService = channelReadService;
        _memoryCache = memoryCache;
        _workspaceContextAccessor = workspaceContextAccessor;
        _readModelCacheInvalidator = readModelCacheInvalidator;
        _logger = logger;
    }

    public async Task<MessagePageResult> GetMessagesPageByChannel(
        string region,
        string channel,
        int offset,
        int take,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(region) || string.IsNullOrWhiteSpace(channel))
        {
            return new MessagePageResult();
        }

        var normalizedRegion = region.Trim();
        var normalizedChannel = channel.Trim();
        var workspaceId = _workspaceContextAccessor.GetWorkspaceId();
        var cacheKey = CreateMessagesCacheKey(workspaceId, normalizedRegion, normalizedChannel, offset, take);

        if (!forceRefresh &&
            _memoryCache.TryGetValue<MessagePageResult>(cacheKey, out var cachedPage) &&
            cachedPage is not null)
        {
            _logger.LogDebug(
                "Returning cached channel message page for workspace {WorkspaceId}, region {Region}, channel {Channel}, offset {Offset}, take {Take}",
                workspaceId,
                normalizedRegion,
                normalizedChannel,
                offset,
                take);
            return cachedPage;
        }

        var page = await _channelReadService.GetMessagesPageByChannel(
            normalizedRegion,
            normalizedChannel,
            offset,
            take,
            cancellationToken);

        _memoryCache.Set(
            cacheKey,
            page,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheDuration,
                Size = 1
            });

        return page;
    }

    public async Task<ChannelSummary> GetChannelSummary(
        string region,
        string channel,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(region) || string.IsNullOrWhiteSpace(channel))
        {
            return new ChannelSummary();
        }

        var normalizedRegion = region.Trim();
        var normalizedChannel = channel.Trim();
        var workspaceId = _workspaceContextAccessor.GetWorkspaceId();
        var cacheKey = CreateSummaryCacheKey(workspaceId, normalizedRegion, normalizedChannel);

        if (!forceRefresh &&
            _memoryCache.TryGetValue<ChannelSummary>(cacheKey, out var cachedSummary) &&
            cachedSummary is not null)
        {
            _logger.LogDebug(
                "Returning cached channel summary for workspace {WorkspaceId}, region {Region}, channel {Channel}",
                workspaceId,
                normalizedRegion,
                normalizedChannel);
            return cachedSummary;
        }

        var summary = await _channelReadService.GetChannelSummary(
            normalizedRegion,
            normalizedChannel,
            cancellationToken);

        _memoryCache.Set(
            cacheKey,
            summary,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheDuration,
                Size = 1
            });

        return summary;
    }

    public async Task<IReadOnlyCollection<ChannelTopNode>> GetTopNodesByChannel(
        string region,
        string channel,
        int take,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(region) || string.IsNullOrWhiteSpace(channel))
        {
            return [];
        }

        var normalizedRegion = region.Trim();
        var normalizedChannel = channel.Trim();
        var workspaceId = _workspaceContextAccessor.GetWorkspaceId();
        var cacheKey = CreateTopNodesCacheKey(workspaceId, normalizedRegion, normalizedChannel, take);

        if (!forceRefresh &&
            _memoryCache.TryGetValue<IReadOnlyCollection<ChannelTopNode>>(cacheKey, out var cachedTopNodes) &&
            cachedTopNodes is not null)
        {
            _logger.LogDebug(
                "Returning cached channel top-nodes for workspace {WorkspaceId}, region {Region}, channel {Channel}, take {Take}",
                workspaceId,
                normalizedRegion,
                normalizedChannel,
                take);
            return cachedTopNodes;
        }

        var topNodes = await _channelReadService.GetTopNodesByChannel(
            normalizedRegion,
            normalizedChannel,
            take,
            cancellationToken);

        _memoryCache.Set(
            cacheKey,
            topNodes,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheDuration,
                Size = 1
            });

        return topNodes;
    }

    private string CreateMessagesCacheKey(string workspaceId, string region, string channel, int offset, int take)
    {
        var stamp = _readModelCacheInvalidator.GetStamp(workspaceId, ReadModelCacheRegion.ChannelDetails);
        return $"channel-messages::{workspaceId}::{stamp}::{region}::{channel}::{offset}::{take}";
    }

    private string CreateSummaryCacheKey(string workspaceId, string region, string channel)
    {
        var stamp = _readModelCacheInvalidator.GetStamp(workspaceId, ReadModelCacheRegion.ChannelDetails);
        return $"channel-summary::{workspaceId}::{stamp}::{region}::{channel}";
    }

    private string CreateTopNodesCacheKey(string workspaceId, string region, string channel, int take)
    {
        var stamp = _readModelCacheInvalidator.GetStamp(workspaceId, ReadModelCacheRegion.ChannelDetails);
        return $"channel-topnodes::{workspaceId}::{stamp}::{region}::{channel}::{take}";
    }
}
