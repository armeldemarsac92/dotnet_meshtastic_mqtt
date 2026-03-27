using MeshBoard.Collector.StatsProjector.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MeshBoard.Collector.StatsProjector.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCollectorStatsProjectorServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IStatsPacketProjectionService, StatsPacketProjectionService>();
        services.AddScoped<IStatsNodeProjectionService, StatsNodeProjectionService>();
        services.AddScoped<IStatsLinkProjectionService, StatsLinkProjectionService>();
        services.AddScoped<IStatsTelemetryProjectionService, StatsTelemetryProjectionService>();

        return services;
    }
}
