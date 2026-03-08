using MeshBoard.Contracts.Topics;

namespace MeshBoard.Application.Abstractions.Persistence;

public interface IDiscoveredTopicRepository
{
    Task<IReadOnlyCollection<TopicCatalogEntry>> GetAllAsync(
        string workspaceId,
        string brokerServer,
        CancellationToken cancellationToken = default);

    Task UpsertAsync(
        string workspaceId,
        string brokerServer,
        string topicPattern,
        string region,
        string channel,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken = default);
}
