using MeshBoard.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MeshBoard.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IBrokerMonitorService, BrokerMonitorService>();
        services.AddScoped<IFavoriteNodeService, FavoriteNodeService>();
        services.AddScoped<IMeshtasticIngestionService, MeshtasticIngestionService>();
        services.AddScoped<IMessageService, MessageService>();
        services.AddScoped<INodeService, NodeService>();
        services.AddScoped<ITopicPresetService, TopicPresetService>();

        return services;
    }
}
