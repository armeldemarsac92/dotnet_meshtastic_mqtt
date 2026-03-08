using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Configuration;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Infrastructure.Meshtastic.Runtime;

internal sealed class BrokerRuntimeBootstrapService : IBrokerRuntimeBootstrapService
{
    private readonly IBrokerServerProfileRepository _brokerServerProfileRepository;
    private readonly IBrokerRuntimeCommandService _brokerRuntimeCommandService;
    private readonly ILogger<BrokerRuntimeBootstrapService> _logger;

    public BrokerRuntimeBootstrapService(
        IBrokerServerProfileRepository brokerServerProfileRepository,
        IBrokerRuntimeCommandService brokerRuntimeCommandService,
        ILogger<BrokerRuntimeBootstrapService> logger)
    {
        _brokerServerProfileRepository = brokerServerProfileRepository;
        _brokerRuntimeCommandService = brokerRuntimeCommandService;
        _logger = logger;
    }

    public async Task InitializeActiveWorkspacesAsync(CancellationToken cancellationToken = default)
    {
        var activeProfiles = await _brokerServerProfileRepository.GetAllActiveAsync(cancellationToken);

        foreach (var registration in activeProfiles)
        {
            await InitializeWorkspaceAsync(registration, cancellationToken);
        }
    }

    private async Task InitializeWorkspaceAsync(
        WorkspaceBrokerServerProfile registration,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Initializing MQTT runtime for workspace {WorkspaceId} with active profile {ProfileId} ({ServerAddress})",
            registration.WorkspaceId,
            registration.Profile.Id,
            registration.Profile.ServerAddress);

        await _brokerRuntimeCommandService.ReconcileActiveProfileAsync(
            registration.WorkspaceId,
            cancellationToken);
    }
}
