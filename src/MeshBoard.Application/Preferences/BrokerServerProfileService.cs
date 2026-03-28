using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Application.Abstractions.Workspaces;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Exceptions;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Application.Preferences;

public interface IBrokerServerProfileService
{
    Task<IReadOnlyCollection<BrokerServerProfile>> GetServerProfiles(CancellationToken cancellationToken = default);

    Task<BrokerServerProfile> GetActiveServerProfile(CancellationToken cancellationToken = default);

    Task<BrokerServerProfile?> GetServerProfileById(Guid profileId, CancellationToken cancellationToken = default);

    Task<BrokerServerProfile> SaveServerProfile(
        SaveBrokerServerProfileRequest request,
        CancellationToken cancellationToken = default);

    Task<BrokerServerProfile> SetActiveServerProfile(Guid profileId, CancellationToken cancellationToken = default);
}

public sealed class BrokerServerProfileService : IBrokerServerProfileService
{
    private readonly ILogger<BrokerServerProfileService> _logger;
    private readonly IBrokerServerProfileRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWorkspaceContextAccessor _workspaceContextAccessor;

    public BrokerServerProfileService(
        IBrokerServerProfileRepository repository,
        IUnitOfWork unitOfWork,
        IWorkspaceContextAccessor workspaceContextAccessor,
        ILogger<BrokerServerProfileService> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _workspaceContextAccessor = workspaceContextAccessor;
        _logger = logger;
    }

    public Task<IReadOnlyCollection<BrokerServerProfile>> GetServerProfiles(CancellationToken cancellationToken = default)
    {
        return _repository.GetAllAsync(GetWorkspaceId(), cancellationToken);
    }

    public async Task<BrokerServerProfile> GetActiveServerProfile(CancellationToken cancellationToken = default)
    {
        var activeProfile = await _repository.GetActiveAsync(GetWorkspaceId(), cancellationToken);

        if (activeProfile is not null)
        {
            return activeProfile;
        }

        _logger.LogWarning("No active broker server profile is configured.");
        throw new NotFoundException("No active broker server profile is configured.");
    }

    public Task<BrokerServerProfile?> GetServerProfileById(Guid profileId, CancellationToken cancellationToken = default)
    {
        return _repository.GetByIdAsync(GetWorkspaceId(), profileId, cancellationToken);
    }

    public async Task<BrokerServerProfile> SaveServerProfile(
        SaveBrokerServerProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspaceId = GetWorkspaceId();
        var normalizedRequest = request.Normalize();

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var upsertRequest = normalizedRequest.Clone();
            upsertRequest.IsActive = false;

            var savedProfile = await _repository.UpsertAsync(workspaceId, upsertRequest, cancellationToken);
            var activeProfile = await _repository.GetActiveAsync(workspaceId, cancellationToken);

            if (normalizedRequest.IsActive || activeProfile is null)
            {
                await _repository.SetExclusiveActiveAsync(workspaceId, savedProfile.Id, cancellationToken);
                savedProfile.IsActive = true;
            }
            else
            {
                savedProfile.IsActive = activeProfile.Id == savedProfile.Id;
            }

            await _unitOfWork.CommitAsync(cancellationToken);
            return savedProfile;
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<BrokerServerProfile> SetActiveServerProfile(Guid profileId, CancellationToken cancellationToken = default)
    {
        var workspaceId = GetWorkspaceId();
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var activatedProfile = await _repository.GetByIdAsync(workspaceId, profileId, cancellationToken);

            if (activatedProfile is null)
            {
                throw new NotFoundException($"Broker server profile '{profileId}' was not found.");
            }

            await _repository.SetExclusiveActiveAsync(workspaceId, profileId, cancellationToken);
            activatedProfile.IsActive = true;

            await _unitOfWork.CommitAsync(cancellationToken);
            return activatedProfile;
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }
    private string GetWorkspaceId()
    {
        return _workspaceContextAccessor.GetWorkspaceId();
    }
}
