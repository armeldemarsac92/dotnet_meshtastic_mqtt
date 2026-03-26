using MeshBoard.Collector.GraphProjector.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MeshBoard.Collector.GraphProjector.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCollectorGraphProjectorServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IGraphNodeProjectionService, GraphNodeProjectionService>();
        services.AddScoped<IGraphLinkProjectionService, GraphLinkProjectionService>();

        return services;
    }
}
