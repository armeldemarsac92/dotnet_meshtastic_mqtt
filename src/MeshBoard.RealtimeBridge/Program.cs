using MeshBoard.RealtimeBridge;
using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Application.DependencyInjection;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Infrastructure.Meshtastic.DependencyInjection;
using MeshBoard.Infrastructure.Persistence.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOptions<RealtimeDownstreamBrokerOptions>()
    .Bind(builder.Configuration.GetSection(RealtimeDownstreamBrokerOptions.SectionName));
builder.Services.AddBridgeApplicationServices();
builder.Services.AddMeshtasticRuntimeInfrastructure(builder.Configuration);
builder.Services.AddProductPersistenceInfrastructure(builder.Configuration);
builder.Services.AddSingleton<IRealtimePacketPublisher, MqttNetRealtimePacketPublisher>();
builder.Services.AddSingleton<IMqttInboundMessageSink, RealtimePacketPublishingSink>();

var host = builder.Build();
await host.RunAsync();
