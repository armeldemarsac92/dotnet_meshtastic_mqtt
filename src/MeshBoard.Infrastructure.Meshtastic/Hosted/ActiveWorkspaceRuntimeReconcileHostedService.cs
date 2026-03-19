using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MeshBoard.Infrastructure.Meshtastic.Hosted;

internal sealed class ActiveWorkspaceRuntimeReconcileHostedService : BackgroundService
{
    private readonly IBrokerRuntimeCommandExecutor _brokerRuntimeCommandExecutor;
    private readonly ILogger<ActiveWorkspaceRuntimeReconcileHostedService> _logger;
    private readonly MeshtasticRuntimeOptions _runtimeOptions;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public ActiveWorkspaceRuntimeReconcileHostedService(
        IServiceScopeFactory serviceScopeFactory,
        IBrokerRuntimeCommandExecutor brokerRuntimeCommandExecutor,
        IOptions<MeshtasticRuntimeOptions> runtimeOptions,
        ILogger<ActiveWorkspaceRuntimeReconcileHostedService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _brokerRuntimeCommandExecutor = brokerRuntimeCommandExecutor;
        _runtimeOptions = runtimeOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reconcileInterval = TimeSpan.FromMilliseconds(
            Math.Max(1_000, _runtimeOptions.ActiveProfileReconcileIntervalMilliseconds));

        using var timer = new PeriodicTimer(reconcileInterval);

        while (!stoppingToken.IsCancellationRequested &&
               await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ReconcileActiveWorkspacesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Reconciling active workspace runtime subscriptions failed.");
            }
        }
    }

    private async Task ReconcileActiveWorkspacesAsync(CancellationToken cancellationToken)
    {
        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var brokerServerProfileRepository = scope.ServiceProvider.GetRequiredService<IBrokerServerProfileRepository>();
        var activeProfiles = await brokerServerProfileRepository.GetAllActiveUserOwnedAsync(cancellationToken);

        foreach (var activeProfile in activeProfiles)
        {
            try
            {
                await _brokerRuntimeCommandExecutor.ReconcileActiveProfileAsync(
                    activeProfile.WorkspaceId,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Reconciling runtime subscriptions failed for workspace {WorkspaceId}",
                    activeProfile.WorkspaceId);
            }
        }
    }
}
