using MeshBoard.Contracts.Topics;

namespace MeshBoard.Application.Abstractions.Persistence;

public interface ITopicPresetRepository
{
    Task<IReadOnlyCollection<TopicPreset>> GetAllAsync(
        string brokerServer,
        CancellationToken cancellationToken = default);

    Task<TopicPreset> UpsertAsync(
        string brokerServer,
        SaveTopicPresetRequest request,
        CancellationToken cancellationToken = default);
}
