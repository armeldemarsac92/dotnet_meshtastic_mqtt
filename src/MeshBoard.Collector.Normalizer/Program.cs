using MeshBoard.Application.DependencyInjection;
using MeshBoard.Collector.Normalizer.Consumers;
using MeshBoard.Collector.Normalizer.DependencyInjection;
using MeshBoard.Infrastructure.Eventing;
using MeshBoard.Infrastructure.Eventing.DependencyInjection;
using MeshBoard.Infrastructure.Meshtastic.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddCollectorApplicationServices();
builder.Services.AddMeshtasticIngestionInfrastructure(builder.Configuration);
builder.Services.AddCollectorNormalizerServices();
builder.Services.AddCollectorEventingInfrastructure(
    builder.Configuration,
    riders => riders.AddCollectorRawPacketsConsumer<RawPacketReceivedConsumer>(
        CollectorConsumerGroups.Normalizer));

var host = builder.Build();
await host.RunAsync();
