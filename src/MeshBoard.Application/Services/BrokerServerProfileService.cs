using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Application.Abstractions.Workspaces;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Exceptions;
using MeshBoard.Contracts.Topics;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Application.Services;

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

        if (request is null)
        {
            throw new BadRequestException("A server profile request is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BadRequestException("Server name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Host))
        {
            throw new BadRequestException("Server host is required.");
        }

        if (request.Port is < 1 or > 65535)
        {
            throw new BadRequestException("Server port must be between 1 and 65535.");
        }

        if (string.IsNullOrWhiteSpace(request.DefaultTopicPattern))
        {
            throw new BadRequestException("A default topic pattern is required.");
        }

        if (string.IsNullOrWhiteSpace(request.DownlinkTopic))
        {
            throw new BadRequestException("A downlink topic is required.");
        }

        if (!TopicEncryptionKey.TryParse(request.DefaultEncryptionKeyBase64, out _))
        {
            throw new BadRequestException("Default encryption key must be valid base64 or hexadecimal AES key.");
        }

        var normalizedRequest = new SaveBrokerServerProfileRequest
        {
            Id = request.Id,
            Name = request.Name.Trim(),
            Host = request.Host.Trim(),
            Port = request.Port,
            UseTls = request.UseTls,
            Username = request.Username.Trim(),
            Password = request.Password,
            DefaultTopicPattern = request.DefaultTopicPattern.Trim(),
            DefaultEncryptionKeyBase64 = TopicEncryptionKey.NormalizeToBase64OrNull(request.DefaultEncryptionKeyBase64)!,
            DownlinkTopic = request.DownlinkTopic.Trim(),
            EnableSend = request.EnableSend,
            IsActive = request.IsActive
        };

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var upsertRequest = CloneRequest(normalizedRequest);
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

    private static SaveBrokerServerProfileRequest CloneRequest(SaveBrokerServerProfileRequest request)
    {
        return new SaveBrokerServerProfileRequest
        {
            Id = request.Id,
            Name = request.Name,
            Host = request.Host,
            Port = request.Port,
            UseTls = request.UseTls,
            Username = request.Username,
            Password = request.Password,
            DefaultTopicPattern = request.DefaultTopicPattern,
            DefaultEncryptionKeyBase64 = request.DefaultEncryptionKeyBase64,
            DownlinkTopic = request.DownlinkTopic,
            EnableSend = request.EnableSend,
            IsActive = request.IsActive
        };
    }

    private string GetWorkspaceId()
    {
        return _workspaceContextAccessor.GetWorkspaceId();
    }
}
