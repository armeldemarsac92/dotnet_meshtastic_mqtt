using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Application.Abstractions.Persistence;
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
    public static IServiceCollection AddMeshtasticCollectorInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        AddMeshtasticOptions(services, configuration);
        AddMeshtasticDirectRuntimeCoreServices(services);
        AddMeshtasticIngestionCoreServices(services);

        if (AreHostedServicesEnabled(configuration))
        {
            AddMeshtasticDirectRuntimeHostedServices(services);
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

    public static IServiceCollection AddMeshtasticStaticBrokerRuntimeInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        AddMeshtasticOptions(services, configuration);
        AddMeshtasticDirectRuntimeCoreServices(services);
        services.TryAddSingleton<IBrokerServerProfileRepository, StaticBrokerServerProfileRepository>();

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

    private static void AddMeshtasticDirectRuntimeCoreServices(IServiceCollection services)
    {
        services.TryAddSingleton<IMqttSessionFactory, MqttSessionFactory>();
        services.TryAddSingleton<IWorkspaceBrokerSessionManager, WorkspaceBrokerSessionManager>();
        services.TryAddSingleton<LocalBrokerRuntimeService>();
        services.TryAddSingleton<IBrokerRuntimeService>(
            serviceProvider => serviceProvider.GetRequiredService<LocalBrokerRuntimeService>());
        services.TryAddSingleton<IBrokerRuntimeBootstrapService, BrokerRuntimeBootstrapService>();
    }

    private static void AddMeshtasticDirectRuntimeHostedServices(IServiceCollection services)
    {
        services.AddHostedService<MeshtasticMqttHostedService>();
        services.AddHostedService<MqttInboundDispatchHostedService>();
        services.AddHostedService<ActiveWorkspaceRuntimeReconcileHostedService>();
    }

    private static bool AreHostedServicesEnabled(IConfiguration configuration)
    {
        var runtimeOptions = configuration
            .GetSection(MeshtasticRuntimeOptions.SectionName)
            .Get<MeshtasticRuntimeOptions>() ?? new MeshtasticRuntimeOptions();

        return runtimeOptions.EnableHostedService;
    }
}
