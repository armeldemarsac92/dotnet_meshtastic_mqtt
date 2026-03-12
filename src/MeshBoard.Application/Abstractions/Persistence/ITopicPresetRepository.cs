using MeshBoard.Contracts.Topics;

namespace MeshBoard.Application.Abstractions.Persistence;

public interface ITopicPresetRepository
{
    Task<IReadOnlyCollection<TopicPreset>> GetAllAsync(
        string workspaceId,
        string brokerServer,
        CancellationToken cancellationToken = default);

    Task<TopicPreset?> GetByTopicPatternAsync(
        string workspaceId,
        string brokerServer,
        string topicPattern,
        CancellationToken cancellationToken = default);

    Task<TopicPreset> UpsertAsync(
        string workspaceId,
        string brokerServer,
        SaveTopicPresetRequest request,
        CancellationToken cancellationToken = default);
}
