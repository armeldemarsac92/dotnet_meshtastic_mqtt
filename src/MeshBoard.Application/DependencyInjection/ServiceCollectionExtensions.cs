using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Application.Meshtastic;
using MeshBoard.Application.Abstractions.Workspaces;
using MeshBoard.Application.Services;
using MeshBoard.Application.Workspaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MeshBoard.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.TryAddSingleton<ITopicEncryptionKeyResolver, NullTopicEncryptionKeyResolver>();
        services.TryAddSingleton<IBrokerRuntimeRegistry, InMemoryBrokerRuntimeRegistry>();
        services.TryAddScoped<IWorkspaceContextAccessor, DefaultWorkspaceContextAccessor>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IBrokerMonitorService, BrokerMonitorService>();
        services.AddScoped<IBrokerRuntimeCommandQueryService, BrokerRuntimeCommandQueryService>();
        services.AddScoped<IBrokerServerProfileService, BrokerServerProfileService>();
        services.AddScoped<IFavoriteNodeService, FavoriteNodeService>();
        services.AddScoped<IMessageComposerService, MessageComposerService>();
        services.AddScoped<IMessageRetentionService, MessageRetentionService>();
        services.AddScoped<IMeshtasticIngestionService, MeshtasticIngestionService>();
        services.AddScoped<IMessageService, MessageService>();
        services.AddScoped<INodeService, NodeService>();
        services.AddScoped<ISendCapabilityService, SendCapabilityService>();
        services.AddScoped<ITopicExplorerService, TopicExplorerService>();
        services.AddScoped<ITopicDiscoveryService, TopicDiscoveryService>();
        services.AddScoped<ITopicProbeService, TopicProbeService>();
        services.AddScoped<ITopicPresetService, TopicPresetService>();

        return services;
    }
}
