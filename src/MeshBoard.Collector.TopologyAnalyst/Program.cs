using MeshBoard.Collector.TopologyAnalyst.DependencyInjection;
using MeshBoard.Infrastructure.Neo4j.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddNeo4jInfrastructure(builder.Configuration);
builder.Services.AddTopologyAnalystServices(builder.Configuration);

var host = builder.Build();
await host.RunAsync();
