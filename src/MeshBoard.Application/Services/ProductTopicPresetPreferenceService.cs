using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Application.Abstractions.Workspaces;
using MeshBoard.Contracts.Exceptions;
using MeshBoard.Contracts.Topics;

namespace MeshBoard.Application.Services;

public interface IProductTopicPresetPreferenceService
{
    Task<IReadOnlyCollection<SavedTopicPreset>> GetTopicPresetPreferences(CancellationToken cancellationToken = default);

    Task<SavedTopicPreset> SaveTopicPresetPreference(
        SaveTopicPresetPreferenceRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ProductTopicPresetPreferenceService : IProductTopicPresetPreferenceService
{
    private readonly IBrokerServerProfileService _brokerServerProfileService;
    private readonly ITopicPresetRepository _topicPresetRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWorkspaceContextAccessor _workspaceContextAccessor;

    public ProductTopicPresetPreferenceService(
        IBrokerServerProfileService brokerServerProfileService,
        ITopicPresetRepository topicPresetRepository,
        IUnitOfWork unitOfWork,
        IWorkspaceContextAccessor workspaceContextAccessor)
    {
        _brokerServerProfileService = brokerServerProfileService;
        _topicPresetRepository = topicPresetRepository;
        _unitOfWork = unitOfWork;
        _workspaceContextAccessor = workspaceContextAccessor;
    }

    public async Task<IReadOnlyCollection<SavedTopicPreset>> GetTopicPresetPreferences(
        CancellationToken cancellationToken = default)
    {
        var workspaceId = _workspaceContextAccessor.GetWorkspaceId();
        var profiles = await _brokerServerProfileService.GetServerProfiles(cancellationToken);
        var savedPresets = new List<SavedTopicPreset>();

        foreach (var profile in profiles)
        {
            var presets = await _topicPresetRepository.GetAllAsync(
                workspaceId,
                profile.Id,
                cancellationToken);

            savedPresets.AddRange(
                presets.Select(
                    preset => preset.ToSavedTopicPreset(
                        profile.Id,
                        profile.Name,
                        profile.ServerAddress)));
        }

        return savedPresets;
    }

    public async Task<SavedTopicPreset> SaveTopicPresetPreference(
        SaveTopicPresetPreferenceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ServerProfileId == Guid.Empty)
        {
            throw new BadRequestException("A server selection is required.");
        }

        var workspaceId = _workspaceContextAccessor.GetWorkspaceId();
        var profile = await _brokerServerProfileService.GetServerProfileById(request.ServerProfileId, cancellationToken);

        if (profile is null)
        {
            throw new NotFoundException($"Server profile '{request.ServerProfileId}' was not found.");
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var preset = await _topicPresetRepository.UpsertAsync(
                workspaceId,
                profile.Id,
                profile.ServerAddress,
                request.ToSaveTopicPresetRequest(),
                cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);

            return preset.ToSavedTopicPreset(profile.Id, profile.Name, profile.ServerAddress);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
