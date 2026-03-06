using MeshBoard.Contracts.Topics;

namespace MeshBoard.Application.Abstractions.Persistence;

public interface IDiscoveredTopicRepository
{
    Task<IReadOnlyCollection<TopicCatalogEntry>> GetAllAsync(
        string brokerServer,
        CancellationToken cancellationToken = default);

    Task UpsertAsync(
        string brokerServer,
        string topicPattern,
        string region,
        string channel,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken = default);
}
