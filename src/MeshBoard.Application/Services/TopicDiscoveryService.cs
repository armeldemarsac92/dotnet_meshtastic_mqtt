using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Application.Abstractions.Workspaces;
using MeshBoard.Contracts.Topics;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Application.Services;

public interface ITopicDiscoveryService
{
    Task<IReadOnlyCollection<TopicCatalogEntry>> GetDiscoveredTopics(CancellationToken cancellationToken = default);

    Task RecordObservedTopic(
        string topicValue,
        DateTimeOffset observedAtUtc,
        string? brokerServer = null,
        CancellationToken cancellationToken = default);
}

public sealed class TopicDiscoveryService : ITopicDiscoveryService
{
    private readonly IBrokerServerProfileService _brokerServerProfileService;
    private readonly IDiscoveredTopicRepository _discoveredTopicRepository;
    private readonly ILogger<TopicDiscoveryService> _logger;
    private readonly ITopicExplorerService _topicExplorerService;
    private readonly IWorkspaceContextAccessor _workspaceContextAccessor;

    public TopicDiscoveryService(
        IBrokerServerProfileService brokerServerProfileService,
        IDiscoveredTopicRepository discoveredTopicRepository,
        ITopicExplorerService topicExplorerService,
        IWorkspaceContextAccessor workspaceContextAccessor,
        ILogger<TopicDiscoveryService> logger)
    {
        _brokerServerProfileService = brokerServerProfileService;
        _discoveredTopicRepository = discoveredTopicRepository;
        _topicExplorerService = topicExplorerService;
        _workspaceContextAccessor = workspaceContextAccessor;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<TopicCatalogEntry>> GetDiscoveredTopics(
        CancellationToken cancellationToken = default)
    {
        var workspaceId = _workspaceContextAccessor.GetWorkspaceId();
        var activeServerAddress = await ResolveActiveServerAddress(cancellationToken);
        _logger.LogDebug("Attempting to fetch discovered topics from persistence");

        return await _discoveredTopicRepository.GetAllAsync(workspaceId, activeServerAddress, cancellationToken);
    }

    public async Task RecordObservedTopic(
        string topicValue,
        DateTimeOffset observedAtUtc,
        string? brokerServer = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(topicValue))
        {
            return;
        }

        var discoveredTopic = _topicExplorerService
            .GetDiscoveredTopics([topicValue])
            .FirstOrDefault();

        if (discoveredTopic is null)
        {
            return;
        }

        _logger.LogDebug("Recording discovered topic {TopicPattern}", discoveredTopic.TopicPattern);

        var workspaceId = _workspaceContextAccessor.GetWorkspaceId();
        var resolvedBrokerServer = string.IsNullOrWhiteSpace(brokerServer)
            ? await ResolveActiveServerAddress(cancellationToken)
            : brokerServer.Trim();

        await _discoveredTopicRepository.UpsertAsync(
            workspaceId,
            resolvedBrokerServer,
            discoveredTopic.TopicPattern,
            discoveredTopic.Region,
            discoveredTopic.Channel,
            observedAtUtc,
            cancellationToken);
    }

    private async Task<string> ResolveActiveServerAddress(CancellationToken cancellationToken)
    {
        var activeProfile = await _brokerServerProfileService.GetActiveServerProfile(cancellationToken);
        return activeProfile.ServerAddress;
    }
}
