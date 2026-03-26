using MeshBoard.Application.DependencyInjection;
using MeshBoard.Infrastructure.Meshtastic.DependencyInjection;
using MeshBoard.Infrastructure.Persistence.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddCollectorApplicationServices();
builder.Services.AddMeshtasticCollectorInfrastructure(builder.Configuration);
builder.Services.AddCollectorPersistenceInfrastructure(builder.Configuration);

var host = builder.Build();
await host.RunAsync();
