using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Infrastructure.Meshtastic.Runtime;

internal sealed class BrokerRuntimeBootstrapService : IBrokerRuntimeBootstrapService
{
    private readonly IBrokerRuntimeCommandExecutor _brokerRuntimeCommandExecutor;
    private readonly ILogger<BrokerRuntimeBootstrapService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public BrokerRuntimeBootstrapService(
        IServiceScopeFactory serviceScopeFactory,
        IBrokerRuntimeCommandExecutor brokerRuntimeCommandExecutor,
        ILogger<BrokerRuntimeBootstrapService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _brokerRuntimeCommandExecutor = brokerRuntimeCommandExecutor;
        _logger = logger;
    }

    public async Task InitializeActiveWorkspacesAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var brokerServerProfileRepository = scope.ServiceProvider.GetRequiredService<IBrokerServerProfileRepository>();
        var activeProfiles = await brokerServerProfileRepository.GetAllActiveUserOwnedAsync(cancellationToken);

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

        await _brokerRuntimeCommandExecutor.ReconcileActiveProfileAsync(
            registration.WorkspaceId,
            cancellationToken);
    }
}
