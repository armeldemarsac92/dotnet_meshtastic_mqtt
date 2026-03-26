using MeshBoard.Application.DependencyInjection;
using MeshBoard.Collector.GraphProjector.Consumers;
using MeshBoard.Collector.GraphProjector.DependencyInjection;
using MeshBoard.Infrastructure.Eventing;
using MeshBoard.Infrastructure.Eventing.DependencyInjection;
using MeshBoard.Infrastructure.Neo4j.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddCollectorApplicationServices();
builder.Services.AddNeo4jInfrastructure(builder.Configuration);
builder.Services.AddCollectorGraphProjectorServices();
builder.Services.AddCollectorEventingInfrastructure(
    builder.Configuration,
    riders =>
    {
        riders.AddCollectorNodeObservedConsumer<NodeObservedConsumer>(CollectorConsumerGroups.GraphProjector);
        riders.AddCollectorLinkObservedConsumer<LinkObservedConsumer>(CollectorConsumerGroups.GraphProjector);
    });

var host = builder.Build();
await host.RunAsync();
