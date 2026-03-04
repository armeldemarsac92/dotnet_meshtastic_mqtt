using MeshBoard.Contracts.Topics;

namespace MeshBoard.Application.Abstractions.Persistence;

public interface IDiscoveredTopicRepository
{
    Task<IReadOnlyCollection<TopicCatalogEntry>> GetAllAsync(CancellationToken cancellationToken = default);

    Task UpsertAsync(
        string topicPattern,
        string region,
        string channel,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken = default);
}
