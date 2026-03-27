using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Collector.Ingress.Observability;
using MeshBoard.Collector.Ingress.Services;
using MeshBoard.Collector.Ingress.Sinks;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MeshBoard.Collector.Ingress.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCollectorIngressServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMqttInboundMessageSink, KafkaRawPacketSink>());
        services.TryAddSingleton<IRawPacketPublisherService, RawPacketPublisherService>();
        services.AddHealthChecks()
            .AddCheck<IngressPublisherHealthCheck>("collector_ingress_raw_packet_publisher");

        return services;
    }
}
