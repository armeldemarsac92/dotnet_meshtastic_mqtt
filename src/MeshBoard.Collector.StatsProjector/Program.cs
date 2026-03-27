using MeshBoard.Application.DependencyInjection;
using MeshBoard.Collector.StatsProjector.Consumers;
using MeshBoard.Collector.StatsProjector.DependencyInjection;
using MeshBoard.Infrastructure.Eventing;
using MeshBoard.Infrastructure.Eventing.DependencyInjection;
using MeshBoard.Infrastructure.Persistence.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddCollectorApplicationServices();
builder.Services.AddCollectorPersistenceInfrastructure(builder.Configuration);
builder.Services.AddCollectorStatsProjectorServices();
builder.Services.AddCollectorEventingInfrastructure(
    builder.Configuration,
    riders =>
    {
        riders.AddCollectorPacketNormalizedConsumer<PacketNormalizedConsumer>(CollectorConsumerGroups.StatsProjector);
        riders.AddCollectorNodeObservedConsumer<NodeObservedConsumer>(CollectorConsumerGroups.StatsProjector);
        riders.AddCollectorLinkObservedConsumer<LinkObservedConsumer>(CollectorConsumerGroups.StatsProjector);
        riders.AddCollectorTelemetryObservedConsumer<TelemetryObservedConsumer>(CollectorConsumerGroups.StatsProjector);
    });

var host = builder.Build();
await host.RunAsync();
