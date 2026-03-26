using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Infrastructure.Meshtastic.Runtime;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Infrastructure.Meshtastic.Hosted;

internal sealed class MeshtasticMqttHostedService : IHostedService
{
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly IBrokerRuntimeBootstrapService _brokerRuntimeBootstrapService;
    private readonly IWorkspaceBrokerSessionManager _brokerSessionManager;
    private readonly ILogger<MeshtasticMqttHostedService> _logger;
    private readonly CancellationTokenSource _stoppingTokenSource = new();
    private Task? _startupTask;

    public MeshtasticMqttHostedService(
        IBrokerRuntimeBootstrapService brokerRuntimeBootstrapService,
        IWorkspaceBrokerSessionManager brokerSessionManager,
        IHostApplicationLifetime applicationLifetime,
        ILogger<MeshtasticMqttHostedService> logger)
    {
        _brokerRuntimeBootstrapService = brokerRuntimeBootstrapService;
        _brokerSessionManager = brokerSessionManager;
        _applicationLifetime = applicationLifetime;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _applicationLifetime.ApplicationStarted.Register(
            () => _startupTask = Task.Run(() => ConnectAndSubscribeAsync(_stoppingTokenSource.Token), CancellationToken.None));

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _stoppingTokenSource.Cancel();

        if (_startupTask is not null)
        {
            try
            {
                await _startupTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        await _brokerSessionManager.DisconnectAllAsync(cancellationToken);
        _stoppingTokenSource.Dispose();
    }

    private async Task ConnectAndSubscribeAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Attempting to start the Meshtastic MQTT hosted service");

        try
        {
            await _brokerRuntimeBootstrapService.InitializeActiveWorkspacesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "The Meshtastic MQTT hosted service could not connect and subscribe during startup");
        }
    }
}
