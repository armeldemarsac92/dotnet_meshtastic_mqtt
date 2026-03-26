using MeshBoard.Collector.TopologyAnalyst.Configuration;
using MeshBoard.Collector.TopologyAnalyst.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MeshBoard.Collector.TopologyAnalyst.Workers;

public sealed class TopologyAnalysisWorker : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly TopologyAnalysisOptions _options;
    private readonly ILogger<TopologyAnalysisWorker> _logger;

    public TopologyAnalysisWorker(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<TopologyAnalysisOptions> options,
        ILogger<TopologyAnalysisWorker> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromSeconds(Math.Max(1, _options.ScheduleIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _serviceScopeFactory.CreateAsyncScope();
                var topologyAnalysisService =
                    scope.ServiceProvider.GetRequiredService<ITopologyAnalysisService>();

                await topologyAnalysisService.RunAnalysisAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Scheduled topology analysis run failed.");
            }

            await Task.Delay(delay, stoppingToken);
        }
    }
}
