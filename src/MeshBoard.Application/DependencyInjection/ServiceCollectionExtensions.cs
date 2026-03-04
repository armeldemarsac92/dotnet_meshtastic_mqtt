using MeshBoard.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MeshBoard.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IBrokerMonitorService, BrokerMonitorService>();
        services.AddScoped<IFavoriteNodeService, FavoriteNodeService>();
        services.AddScoped<IMessageComposerService, MessageComposerService>();
        services.AddScoped<IMessageRetentionService, MessageRetentionService>();
        services.AddScoped<IMeshtasticIngestionService, MeshtasticIngestionService>();
        services.AddScoped<IMessageService, MessageService>();
        services.AddScoped<INodeService, NodeService>();
        services.AddScoped<ISendCapabilityService, SendCapabilityService>();
        services.AddScoped<ITopicPresetService, TopicPresetService>();

        return services;
    }
}
