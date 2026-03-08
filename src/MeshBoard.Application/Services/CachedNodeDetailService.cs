using MeshBoard.Application.Abstractions.Workspaces;
using MeshBoard.Application.Caching;
using MeshBoard.Contracts.Messages;
using MeshBoard.Contracts.Nodes;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Application.Services;

public interface ICachedNodeDetailService
{
    Task<NodeSummary?> GetNodeById(
        string nodeId,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default);

    Task<MessagePageResult> GetMessagesPageBySender(
        string senderNodeId,
        int offset,
        int take,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default);
}

public sealed class CachedNodeDetailService : ICachedNodeDetailService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(15);

    private readonly ILogger<CachedNodeDetailService> _logger;
    private readonly IMessageService _messageService;
    private readonly IMemoryCache _memoryCache;
    private readonly INodeService _nodeService;
    private readonly IReadModelCacheInvalidator _readModelCacheInvalidator;
    private readonly IWorkspaceContextAccessor _workspaceContextAccessor;

    public CachedNodeDetailService(
        IMessageService messageService,
        IMemoryCache memoryCache,
        INodeService nodeService,
        IWorkspaceContextAccessor workspaceContextAccessor,
        IReadModelCacheInvalidator readModelCacheInvalidator,
        ILogger<CachedNodeDetailService> logger)
    {
        _messageService = messageService;
        _memoryCache = memoryCache;
        _nodeService = nodeService;
        _workspaceContextAccessor = workspaceContextAccessor;
        _readModelCacheInvalidator = readModelCacheInvalidator;
        _logger = logger;
    }

    public async Task<NodeSummary?> GetNodeById(
        string nodeId,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return null;
        }

        var normalizedNodeId = nodeId.Trim();
        var workspaceId = _workspaceContextAccessor.GetWorkspaceId();
        var cacheKey = CreateNodeCacheKey(workspaceId, normalizedNodeId);

        if (!forceRefresh &&
            _memoryCache.TryGetValue<NodeSummary?>(cacheKey, out var cachedNode))
        {
            _logger.LogDebug(
                "Returning cached node detail for workspace {WorkspaceId} and node {NodeId}",
                workspaceId,
                normalizedNodeId);
            return cachedNode;
        }

        var node = await _nodeService.GetNodeById(normalizedNodeId, cancellationToken);

        _memoryCache.Set(
            cacheKey,
            node,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheDuration,
                Size = 1
            });

        return node;
    }

    public async Task<MessagePageResult> GetMessagesPageBySender(
        string senderNodeId,
        int offset,
        int take,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(senderNodeId))
        {
            return new MessagePageResult();
        }

        var normalizedSenderNodeId = senderNodeId.Trim();
        var workspaceId = _workspaceContextAccessor.GetWorkspaceId();
        var cacheKey = CreateSenderMessagesCacheKey(workspaceId, normalizedSenderNodeId, offset, take);

        if (!forceRefresh &&
            _memoryCache.TryGetValue<MessagePageResult>(cacheKey, out var cachedPage) &&
            cachedPage is not null)
        {
            _logger.LogDebug(
                "Returning cached node message page for workspace {WorkspaceId}, node {NodeId}, offset {Offset}, take {Take}",
                workspaceId,
                normalizedSenderNodeId,
                offset,
                take);
            return cachedPage;
        }

        var page = await _messageService.GetMessagesPageBySender(
            normalizedSenderNodeId,
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

    private string CreateNodeCacheKey(string workspaceId, string nodeId)
    {
        var stamp = _readModelCacheInvalidator.GetStamp(workspaceId, ReadModelCacheRegion.NodeDetails);
        return $"node::{workspaceId}::{stamp}::{nodeId}";
    }

    private string CreateSenderMessagesCacheKey(string workspaceId, string nodeId, int offset, int take)
    {
        var stamp = _readModelCacheInvalidator.GetStamp(workspaceId, ReadModelCacheRegion.NodeDetails);
        return $"node-messages::{workspaceId}::{stamp}::{nodeId}::{offset}::{take}";
    }
}
