using MeshBoard.Application.DependencyInjection;
using MeshBoard.Collector.Ingress.DependencyInjection;
using MeshBoard.Infrastructure.Eventing.DependencyInjection;
using MeshBoard.Infrastructure.Meshtastic.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddCollectorApplicationServices();
builder.Services.AddMeshtasticRuntimeInfrastructure(builder.Configuration);
builder.Services.AddCollectorIngressServices();
builder.Services.AddCollectorEventingInfrastructure(
    builder.Configuration,
    riders => { });

var host = builder.Build();
await host.RunAsync();
