using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Topics;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Application.Services;

public interface ITopicDiscoveryService
{
    Task<IReadOnlyCollection<TopicCatalogEntry>> GetDiscoveredTopics(CancellationToken cancellationToken = default);

    Task RecordObservedTopic(
        string topicValue,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken = default);
}

public sealed class TopicDiscoveryService : ITopicDiscoveryService
{
    private readonly IDiscoveredTopicRepository _discoveredTopicRepository;
    private readonly ILogger<TopicDiscoveryService> _logger;
    private readonly ITopicExplorerService _topicExplorerService;

    public TopicDiscoveryService(
        IDiscoveredTopicRepository discoveredTopicRepository,
        ITopicExplorerService topicExplorerService,
        ILogger<TopicDiscoveryService> logger)
    {
        _discoveredTopicRepository = discoveredTopicRepository;
        _topicExplorerService = topicExplorerService;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<TopicCatalogEntry>> GetDiscoveredTopics(
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Attempting to fetch discovered topics from persistence");

        return await _discoveredTopicRepository.GetAllAsync(cancellationToken);
    }

    public async Task RecordObservedTopic(
        string topicValue,
        DateTimeOffset observedAtUtc,
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

        await _discoveredTopicRepository.UpsertAsync(
            discoveredTopic.TopicPattern,
            discoveredTopic.Region,
            discoveredTopic.Channel,
            observedAtUtc,
            cancellationToken);
    }
}
