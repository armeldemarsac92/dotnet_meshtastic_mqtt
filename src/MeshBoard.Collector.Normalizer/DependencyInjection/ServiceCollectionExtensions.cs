using MeshBoard.Collector.Normalizer.Services;

namespace MeshBoard.Collector.Normalizer.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCollectorNormalizerServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IPacketNormalizationService, PacketNormalizationService>();

        return services;
    }
}
