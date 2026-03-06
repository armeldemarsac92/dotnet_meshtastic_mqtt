using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Exceptions;
using MeshBoard.Contracts.Topics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MeshBoard.Application.Services;

public interface IBrokerServerProfileService
{
    Task<IReadOnlyCollection<BrokerServerProfile>> GetServerProfiles(CancellationToken cancellationToken = default);

    Task<BrokerServerProfile> GetActiveServerProfile(CancellationToken cancellationToken = default);

    Task<BrokerServerProfile> SaveServerProfile(
        SaveBrokerServerProfileRequest request,
        CancellationToken cancellationToken = default);

    Task<BrokerServerProfile> SetActiveServerProfile(Guid profileId, CancellationToken cancellationToken = default);
}

public sealed class BrokerServerProfileService : IBrokerServerProfileService
{
    private readonly BrokerOptions _brokerOptions;
    private readonly ILogger<BrokerServerProfileService> _logger;
    private readonly IBrokerServerProfileRepository _repository;

    public BrokerServerProfileService(
        IBrokerServerProfileRepository repository,
        IOptions<BrokerOptions> brokerOptions,
        ILogger<BrokerServerProfileService> logger)
    {
        _repository = repository;
        _brokerOptions = brokerOptions.Value;
        _logger = logger;
    }

    public Task<IReadOnlyCollection<BrokerServerProfile>> GetServerProfiles(CancellationToken cancellationToken = default)
    {
        return _repository.GetAllAsync(cancellationToken);
    }

    public async Task<BrokerServerProfile> GetActiveServerProfile(CancellationToken cancellationToken = default)
    {
        var activeProfile = await _repository.GetActiveAsync(cancellationToken);

        if (activeProfile is not null)
        {
            return activeProfile;
        }

        _logger.LogInformation("No active broker server profile found. Seeding default profile from app settings.");

        var seededProfile = await SaveServerProfile(
            new SaveBrokerServerProfileRequest
            {
                Name = "Default server",
                Host = _brokerOptions.Host,
                Port = _brokerOptions.Port,
                UseTls = _brokerOptions.UseTls,
                Username = _brokerOptions.Username,
                Password = _brokerOptions.Password,
                DefaultTopicPattern = _brokerOptions.DefaultTopicPattern,
                DefaultEncryptionKeyBase64 = _brokerOptions.DefaultEncryptionKeyBase64,
                DownlinkTopic = _brokerOptions.DownlinkTopic,
                EnableSend = _brokerOptions.EnableSend,
                IsActive = true
            },
            cancellationToken);

        return seededProfile;
    }

    public async Task<BrokerServerProfile> SaveServerProfile(
        SaveBrokerServerProfileRequest request,
        CancellationToken cancellationToken = default)
    {
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

        if (normalizedRequest.IsActive)
        {
            await _repository.ClearActiveAsync(cancellationToken);
        }

        var savedProfile = await _repository.UpsertAsync(normalizedRequest, cancellationToken);

        var activeProfile = await _repository.GetActiveAsync(cancellationToken);

        if (activeProfile is null)
        {
            await _repository.SetActiveAsync(savedProfile.Id, cancellationToken);
            savedProfile.IsActive = true;
        }

        return savedProfile;
    }

    public async Task<BrokerServerProfile> SetActiveServerProfile(Guid profileId, CancellationToken cancellationToken = default)
    {
        await _repository.ClearActiveAsync(cancellationToken);
        await _repository.SetActiveAsync(profileId, cancellationToken);

        var activatedProfile = await _repository.GetByIdAsync(profileId, cancellationToken);

        if (activatedProfile is null)
        {
            throw new NotFoundException($"Broker server profile '{profileId}' was not found.");
        }

        return activatedProfile;
    }
}
