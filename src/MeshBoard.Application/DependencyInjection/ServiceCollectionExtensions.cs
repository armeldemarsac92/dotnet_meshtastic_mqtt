using MeshBoard.Application.Abstractions.Collector;
using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Application.Abstractions.Realtime;
using MeshBoard.Application.Meshtastic;
using MeshBoard.Application.Abstractions.Workspaces;
using MeshBoard.Application.Authentication;
using MeshBoard.Application.Caching;
using MeshBoard.Application.Collector;
using MeshBoard.Application.Observability;
using MeshBoard.Application.Preferences;
using MeshBoard.Application.Realtime;
using MeshBoard.Application.Topics;
using MeshBoard.Application.Workspaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MeshBoard.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBridgeApplicationServices(this IServiceCollection services)
    {
        services.TryAddSingleton<IBrokerRuntimeRegistry, InMemoryBrokerRuntimeRegistry>();
        services.TryAddSingleton<IRealtimePacketEnvelopeFactory, RealtimePacketEnvelopeFactory>();
        services.TryAddSingleton<IRealtimePacketPublicationFactory, RealtimePacketPublicationFactory>();

        return services;
    }

    public static IServiceCollection AddCollectorApplicationServices(this IServiceCollection services)
    {
        services.AddMemoryCache(options => options.SizeLimit = 1_024);
        services.TryAddSingleton<ILinkDerivationService, LinkDerivationService>();
        services.TryAddSingleton<ICollectorChannelResolver, CollectorChannelResolver>();
        services.TryAddSingleton<ITopicEncryptionKeyResolver, NullTopicEncryptionKeyResolver>();
        services.TryAddSingleton<IBrokerRuntimeRegistry, InMemoryBrokerRuntimeRegistry>();
        services.TryAddScoped<IWorkspaceContextAccessor, DefaultWorkspaceContextAccessor>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IBrokerServerProfileService, BrokerServerProfileService>();
        services.AddScoped<IMeshtasticIngestionService, MeshtasticIngestionService>();
        services.AddScoped<ITopicExplorerService, TopicExplorerService>();
        services.AddScoped<ITopicDiscoveryService, TopicDiscoveryService>();

        return services;
    }

    public static IServiceCollection AddApiApplicationServices(this IServiceCollection services)
    {
        services.TryAddSingleton<ITopicEncryptionKeyResolver, NullTopicEncryptionKeyResolver>();
        services.TryAddSingleton<IPasswordHashingService, PasswordHashingService>();
        services.TryAddSingleton<IRealtimeJwksService, RealtimeJwksService>();
        services.TryAddSingleton<IRealtimeTopicAccessPolicyService, RealtimeTopicAccessPolicyService>();
        services.TryAddSingleton<IRealtimeTopicFilterAuthorizationService, RealtimeTopicFilterAuthorizationService>();
        services.TryAddSingleton<IVernemqWebhookAuthorizationService, VernemqWebhookAuthorizationService>();
        services.TryAddScoped<IWorkspaceContextAccessor, DefaultWorkspaceContextAccessor>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IBrokerServerProfileService, BrokerServerProfileService>();
        services.AddScoped<IFavoriteNodeService, FavoriteNodeService>();
        services.AddScoped<IProductBrokerPreferenceService, ProductBrokerPreferenceService>();
        services.AddScoped<IUserAccountService, UserAccountService>();
        services.AddScoped<IWorkspaceProvisioningService, WorkspaceProvisioningService>();

        return services;
    }

    public static IServiceCollection AddCollectorReadApplicationServices(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ICollectorReadService, CollectorReadService>();

        return services;
    }

}
