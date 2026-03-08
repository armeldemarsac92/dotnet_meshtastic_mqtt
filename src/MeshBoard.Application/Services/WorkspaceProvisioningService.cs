using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Topics;
using MeshBoard.Contracts.Workspaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MeshBoard.Application.Services;

public interface IWorkspaceProvisioningService
{
    Task ProvisionAsync(string workspaceId, CancellationToken cancellationToken = default);
}

public sealed class WorkspaceProvisioningService : IWorkspaceProvisioningService
{
    private readonly BrokerOptions _brokerOptions;
    private readonly IBrokerServerProfileRepository _brokerServerProfileRepository;
    private readonly ILogger<WorkspaceProvisioningService> _logger;
    private readonly ITopicPresetRepository _topicPresetRepository;

    public WorkspaceProvisioningService(
        IBrokerServerProfileRepository brokerServerProfileRepository,
        ITopicPresetRepository topicPresetRepository,
        IOptions<BrokerOptions> brokerOptions,
        ILogger<WorkspaceProvisioningService> logger)
    {
        _brokerServerProfileRepository = brokerServerProfileRepository;
        _topicPresetRepository = topicPresetRepository;
        _brokerOptions = brokerOptions.Value;
        _logger = logger;
    }

    public async Task ProvisionAsync(string workspaceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            throw new InvalidOperationException("Workspace id is required to provision workspace defaults.");
        }

        if (string.Equals(workspaceId, WorkspaceConstants.DefaultWorkspaceId, StringComparison.Ordinal))
        {
            return;
        }

        var existingProfiles = await _brokerServerProfileRepository.GetAllAsync(workspaceId, cancellationToken);
        if (existingProfiles.Count > 0)
        {
            return;
        }

        var normalizedDefaultKey = MeshBoard.Contracts.Topics.TopicEncryptionKey.NormalizeToBase64OrNull(_brokerOptions.DefaultEncryptionKeyBase64) ??
            MeshBoard.Contracts.Topics.TopicEncryptionKey.DefaultKeyBase64;

        var defaultProfile = await _brokerServerProfileRepository.UpsertAsync(
            workspaceId,
            new SaveBrokerServerProfileRequest
            {
                Name = "Default server",
                Host = _brokerOptions.Host,
                Port = _brokerOptions.Port,
                UseTls = _brokerOptions.UseTls,
                Username = _brokerOptions.Username,
                Password = _brokerOptions.Password,
                DefaultTopicPattern = _brokerOptions.DefaultTopicPattern,
                DefaultEncryptionKeyBase64 = normalizedDefaultKey,
                DownlinkTopic = _brokerOptions.DownlinkTopic,
                EnableSend = _brokerOptions.EnableSend,
                IsActive = true
            },
            cancellationToken);

        await _topicPresetRepository.UpsertAsync(
            workspaceId,
            defaultProfile.ServerAddress,
            new SaveTopicPresetRequest
            {
                Name = "US Public Feed",
                TopicPattern = _brokerOptions.DefaultTopicPattern,
                IsDefault = true
            },
            cancellationToken);

        await _topicPresetRepository.UpsertAsync(
            workspaceId,
            defaultProfile.ServerAddress,
            new SaveTopicPresetRequest
            {
                Name = "EU Public Feed",
                TopicPattern = "msh/EU_433/2/e/#",
                IsDefault = false
            },
            cancellationToken);

        _logger.LogInformation(
            "Provisioned default broker profile and presets for workspace {WorkspaceId}",
            workspaceId);
    }
}
