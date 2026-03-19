using MeshBoard.Contracts.Topics;

namespace MeshBoard.Application.Abstractions.Persistence;

public interface ITopicPresetRepository
{
    Task<IReadOnlyCollection<TopicPreset>> GetAllAsync(
        string workspaceId,
        Guid brokerServerProfileId,
        CancellationToken cancellationToken = default);

    Task<TopicPreset?> GetByTopicPatternAsync(
        string workspaceId,
        Guid brokerServerProfileId,
        string topicPattern,
        CancellationToken cancellationToken = default);

    Task<TopicPreset> UpsertAsync(
        string workspaceId,
        Guid brokerServerProfileId,
        string brokerServer,
        SaveTopicPresetRequest request,
        CancellationToken cancellationToken = default);
}
