using MeshBoard.Collector.TopologyAnalyst.Configuration;
using MeshBoard.Collector.TopologyAnalyst.Services;
using MeshBoard.Collector.TopologyAnalyst.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MeshBoard.Collector.TopologyAnalyst.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTopologyAnalystServices(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<TopologyAnalysisOptions>(configuration.GetSection(TopologyAnalysisOptions.SectionName));
        services.AddScoped<ITopologyAnalysisService, TopologyAnalysisService>();
        services.AddHostedService<TopologyAnalysisWorker>();

        return services;
    }
}
