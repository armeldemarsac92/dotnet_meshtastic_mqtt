using MeshBoard.Application.Caching;
using MeshBoard.Application.Abstractions.Workspaces;
using MeshBoard.Contracts.Messages;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Application.Services;

public interface ICachedMessagePageService
{
    Task<MessagePageResult> GetMessagesPage(
        MessageQuery query,
        int offset,
        int take,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default);
}

public sealed class CachedMessagePageService : ICachedMessagePageService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(10);

    private readonly ILogger<CachedMessagePageService> _logger;
    private readonly IMessageService _messageService;
    private readonly IMemoryCache _memoryCache;
    private readonly IReadModelCacheInvalidator _readModelCacheInvalidator;
    private readonly IWorkspaceContextAccessor _workspaceContextAccessor;

    public CachedMessagePageService(
        IMessageService messageService,
        IMemoryCache memoryCache,
        IWorkspaceContextAccessor workspaceContextAccessor,
        IReadModelCacheInvalidator readModelCacheInvalidator,
        ILogger<CachedMessagePageService> logger)
    {
        _messageService = messageService;
        _memoryCache = memoryCache;
        _workspaceContextAccessor = workspaceContextAccessor;
        _readModelCacheInvalidator = readModelCacheInvalidator;
        _logger = logger;
    }

    public async Task<MessagePageResult> GetMessagesPage(
        MessageQuery query,
        int offset,
        int take,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var workspaceId = _workspaceContextAccessor.GetWorkspaceId();
        var cacheKey = CreateCacheKey(workspaceId, query, offset, take);

        if (!forceRefresh &&
            _memoryCache.TryGetValue<MessagePageResult>(cacheKey, out var cachedPage) &&
            cachedPage is not null)
        {
            _logger.LogDebug(
                "Returning cached message page for workspace {WorkspaceId} with offset {Offset} and take {Take}",
                workspaceId,
                offset,
                take);
            return cachedPage;
        }

        var page = await _messageService.GetMessagesPage(query, offset, take, cancellationToken);

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

    private string CreateCacheKey(string workspaceId, MessageQuery query, int offset, int take)
    {
        var stamp = _readModelCacheInvalidator.GetStamp(workspaceId, ReadModelCacheRegion.MessagePages);

        return string.Join(
            "::",
            "messages",
            workspaceId,
            stamp.ToString(),
            offset.ToString(),
            take.ToString(),
            Normalize(query.BrokerServer),
            Normalize(query.SearchText),
            Normalize(query.PacketType),
            ((int)query.Visibility).ToString());
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
