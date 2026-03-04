using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Topics;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Application.Services;

public interface ITopicPresetService
{
    Task<IReadOnlyCollection<TopicPreset>> GetTopicPresets(CancellationToken cancellationToken = default);

    Task<TopicPreset> SaveTopicPreset(SaveTopicPresetRequest request, CancellationToken cancellationToken = default);
}

public sealed class TopicPresetService : ITopicPresetService
{
    private readonly ILogger<TopicPresetService> _logger;
    private readonly ITopicPresetRepository _topicPresetRepository;
    private readonly IUnitOfWork _unitOfWork;

    public TopicPresetService(
        ITopicPresetRepository topicPresetRepository,
        IUnitOfWork unitOfWork,
        ILogger<TopicPresetService> logger)
    {
        _topicPresetRepository = topicPresetRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<TopicPreset>> GetTopicPresets(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting to get topic presets");

        var topicPresets = await _topicPresetRepository.GetAllAsync(cancellationToken);

        _logger.LogInformation("Retrieved {TopicPresetCount} topic presets", topicPresets.Count);

        return topicPresets;
    }

    public async Task<TopicPreset> SaveTopicPreset(
        SaveTopicPresetRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting to save topic preset: {TopicPattern}", request.TopicPattern);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var topicPreset = await _topicPresetRepository.UpsertAsync(request, cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);

            _logger.LogInformation("Saved topic preset: {TopicPattern}", topicPreset.TopicPattern);

            return topicPreset;
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
