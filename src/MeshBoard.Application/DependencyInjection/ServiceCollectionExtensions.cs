using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Application.Abstractions.Realtime;
using MeshBoard.Application.Meshtastic;
using MeshBoard.Application.Abstractions.Workspaces;
using MeshBoard.Application.Authentication;
using MeshBoard.Application.Caching;
using MeshBoard.Application.Observability;
using MeshBoard.Application.Services;
using MeshBoard.Application.Workspaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MeshBoard.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddMemoryCache(options => options.SizeLimit = 1_024);
        services.TryAddSingleton<ITopicEncryptionKeyResolver, NullTopicEncryptionKeyResolver>();
        services.TryAddSingleton<IRealtimePacketEnvelopeFactory, RealtimePacketEnvelopeFactory>();
        services.TryAddSingleton<IRealtimePacketPublicationFactory, RealtimePacketPublicationFactory>();
        services.TryAddSingleton<IBrokerRuntimeRegistry, InMemoryBrokerRuntimeRegistry>();
        services.TryAddSingleton<IActiveCircuitMetricsService, InMemoryActiveCircuitMetricsService>();
        services.TryAddSingleton<IPasswordHashingService, PasswordHashingService>();
        services.TryAddSingleton<IReadModelCacheInvalidator, InMemoryReadModelCacheInvalidator>();
        services.TryAddSingleton<IReadModelMetricsService, InMemoryReadModelMetricsService>();
        services.TryAddScoped<IWorkspaceContextAccessor, DefaultWorkspaceContextAccessor>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IBrokerRuntimeCommandQueryService, BrokerRuntimeCommandQueryService>();
        services.AddScoped<IBrokerServerProfileService, BrokerServerProfileService>();
        services.AddScoped<ICachedChannelDetailService, CachedChannelDetailService>();
        services.AddScoped<ICachedMessagePageService, CachedMessagePageService>();
        services.AddScoped<ICachedNodeDetailService, CachedNodeDetailService>();
        services.AddScoped<IChannelReadService, ChannelReadService>();
        services.AddScoped<IFavoriteNodeService, FavoriteNodeService>();
        services.AddScoped<IMessageComposerService, MessageComposerService>();
        services.AddScoped<IMessageRetentionService, MessageRetentionService>();
        services.AddScoped<IMeshtasticIngestionService, MeshtasticIngestionService>();
        services.AddScoped<IMessageService, MessageService>();
        services.AddScoped<INodeService, NodeService>();
        services.AddScoped<ISavedChannelPreferenceService, SavedChannelPreferenceService>();
        services.AddScoped<ISendCapabilityService, SendCapabilityService>();
        services.AddScoped<ITopicExplorerService, TopicExplorerService>();
        services.AddScoped<ITopicDiscoveryService, TopicDiscoveryService>();
        services.AddScoped<ITopicPresetService, TopicPresetService>();
        services.AddScoped<IUserAccountService, UserAccountService>();
        services.AddScoped<IWorkspaceProvisioningService, WorkspaceProvisioningService>();

        return services;
    }
}
