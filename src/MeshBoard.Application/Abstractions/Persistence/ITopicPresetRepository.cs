using MeshBoard.Contracts.Topics;

namespace MeshBoard.Application.Abstractions.Persistence;

public interface ITopicPresetRepository
{
    Task<IReadOnlyCollection<TopicPreset>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<TopicPreset> UpsertAsync(SaveTopicPresetRequest request, CancellationToken cancellationToken = default);
}
