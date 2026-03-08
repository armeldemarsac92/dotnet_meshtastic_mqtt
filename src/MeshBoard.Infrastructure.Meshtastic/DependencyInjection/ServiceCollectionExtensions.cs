using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Infrastructure.Meshtastic.Decoding;
using MeshBoard.Infrastructure.Meshtastic.Hosted;
using MeshBoard.Infrastructure.Meshtastic.Mqtt;
using MeshBoard.Infrastructure.Meshtastic.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MeshBoard.Infrastructure.Meshtastic.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMeshtasticInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<BrokerOptions>()
            .Bind(configuration.GetSection(BrokerOptions.SectionName));

        services.AddSingleton<ITopicEncryptionKeyResolver, TopicPresetEncryptionKeyResolver>();
        services.AddSingleton<IMeshtasticEnvelopeReader, MeshtasticEnvelopeReader>();
        services.AddSingleton<IMqttSessionFactory, MqttSessionFactory>();
        services.AddSingleton<IWorkspaceBrokerSessionManager, WorkspaceBrokerSessionManager>();
        services.AddSingleton<IBrokerRuntimeBootstrapService, BrokerRuntimeBootstrapService>();
        services.AddHostedService<MeshtasticMqttHostedService>();

        return services;
    }
}
