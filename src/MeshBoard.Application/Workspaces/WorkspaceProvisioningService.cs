using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Workspaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MeshBoard.Application.Workspaces;

public interface IWorkspaceProvisioningService
{
    Task ProvisionAsync(string workspaceId, CancellationToken cancellationToken = default);
}

public sealed class WorkspaceProvisioningService : IWorkspaceProvisioningService
{
    private readonly BrokerOptions _brokerOptions;
    private readonly IBrokerServerProfileRepository _brokerServerProfileRepository;
    private readonly ILogger<WorkspaceProvisioningService> _logger;

    public WorkspaceProvisioningService(
        IBrokerServerProfileRepository brokerServerProfileRepository,
        IOptions<BrokerOptions> brokerOptions,
        ILogger<WorkspaceProvisioningService> logger)
    {
        _brokerServerProfileRepository = brokerServerProfileRepository;
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

        await _brokerServerProfileRepository.UpsertAsync(
            workspaceId,
            _brokerOptions.ToDefaultSaveBrokerServerProfileRequest(),
            cancellationToken);

        _logger.LogInformation(
            "Provisioned default broker profile for workspace {WorkspaceId}",
            workspaceId);
    }
}
