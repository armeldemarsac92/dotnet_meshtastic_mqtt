using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Infrastructure.Meshtastic.Decoding;
using MeshBoard.Infrastructure.Meshtastic.Hosted;
using MeshBoard.Infrastructure.Meshtastic.Mqtt;
using MeshBoard.Infrastructure.Meshtastic.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MeshBoard.Infrastructure.Meshtastic.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMeshtasticInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        AddMeshtasticOptions(services, configuration);
        AddMeshtasticQueuedRuntimeCoreServices(services);
        AddMeshtasticIngestionCoreServices(services);

        if (AreHostedServicesEnabled(configuration))
        {
            AddMeshtasticQueuedRuntimeHostedServices(services);
            AddMeshtasticIngestionHostedServices(services);
        }

        return services;
    }

    public static IServiceCollection AddMeshtasticRuntimeInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        AddMeshtasticOptions(services, configuration);
        AddMeshtasticDirectRuntimeCoreServices(services);

        if (AreHostedServicesEnabled(configuration))
        {
            AddMeshtasticDirectRuntimeHostedServices(services);
        }

        return services;
    }

    public static IServiceCollection AddMeshtasticIngestionInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        AddMeshtasticOptions(services, configuration);
        AddMeshtasticIngestionCoreServices(services);

        if (AreHostedServicesEnabled(configuration))
        {
            AddMeshtasticIngestionHostedServices(services);
        }

        return services;
    }

    private static void AddMeshtasticIngestionCoreServices(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<ITopicEncryptionKeyResolver, TopicPresetEncryptionKeyResolver>());
        services.TryAddSingleton<IMeshtasticEnvelopeReader, MeshtasticEnvelopeReader>();
        services.TryAddSingleton<MeshtasticInboundMessageQueue>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMqttInboundMessageSink, MeshtasticInboundQueueSink>());
    }

    private static void AddMeshtasticIngestionHostedServices(IServiceCollection services)
    {
        services.AddHostedService<MeshtasticInboundProcessingHostedService>();
        services.AddHostedService<MeshtasticRuntimeMetricsHostedService>();
    }

    private static void AddMeshtasticOptions(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<BrokerOptions>()
            .Bind(configuration.GetSection(BrokerOptions.SectionName));
        services
            .AddOptions<MeshtasticRuntimeOptions>()
            .Bind(configuration.GetSection(MeshtasticRuntimeOptions.SectionName));
    }

    private static void AddMeshtasticQueuedRuntimeCoreServices(IServiceCollection services)
    {
        services.TryAddSingleton<IMqttSessionFactory, MqttSessionFactory>();
        services.TryAddSingleton<IWorkspaceBrokerSessionManager, WorkspaceBrokerSessionManager>();
        services.TryAddSingleton<IBrokerRuntimeCommandExecutor, LocalBrokerRuntimeCommandService>();
        services.TryAddSingleton<IBrokerRuntimeCommandService, QueuedBrokerRuntimeCommandService>();
        services.TryAddSingleton<IBrokerRuntimeBootstrapService, BrokerRuntimeBootstrapService>();
    }

    private static void AddMeshtasticDirectRuntimeCoreServices(IServiceCollection services)
    {
        services.TryAddSingleton<IMqttSessionFactory, MqttSessionFactory>();
        services.TryAddSingleton<IWorkspaceBrokerSessionManager, WorkspaceBrokerSessionManager>();
        services.TryAddSingleton<LocalBrokerRuntimeCommandService>();
        services.TryAddSingleton<IBrokerRuntimeCommandExecutor>(
            serviceProvider => serviceProvider.GetRequiredService<LocalBrokerRuntimeCommandService>());
        services.TryAddSingleton<IBrokerRuntimeCommandService>(
            serviceProvider => serviceProvider.GetRequiredService<LocalBrokerRuntimeCommandService>());
        services.TryAddSingleton<IBrokerRuntimeBootstrapService, BrokerRuntimeBootstrapService>();
    }

    private static void AddMeshtasticQueuedRuntimeHostedServices(IServiceCollection services)
    {
        services.AddHostedService<MeshtasticMqttHostedService>();
        services.AddHostedService<MqttInboundDispatchHostedService>();
        services.AddHostedService<BrokerRuntimeCommandProcessorHostedService>();
    }

    private static void AddMeshtasticDirectRuntimeHostedServices(IServiceCollection services)
    {
        services.AddHostedService<MeshtasticMqttHostedService>();
        services.AddHostedService<MqttInboundDispatchHostedService>();
    }

    private static bool AreHostedServicesEnabled(IConfiguration configuration)
    {
        var runtimeOptions = configuration
            .GetSection(MeshtasticRuntimeOptions.SectionName)
            .Get<MeshtasticRuntimeOptions>() ?? new MeshtasticRuntimeOptions();

        return runtimeOptions.EnableHostedService;
    }
}
