using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Infrastructure.Persistence.Context;
using MeshBoard.Infrastructure.Persistence.Initialization;
using MeshBoard.Infrastructure.Persistence.Repositories;
using MeshBoard.Infrastructure.Persistence.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MeshBoard.Infrastructure.Persistence.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPersistenceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var provider = configuration.GetSection(PersistenceOptions.SectionName).GetValue<string>("Provider");

        if (!string.Equals(provider, "SQLite", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                $"The configured persistence provider '{provider}' is not supported yet. Only SQLite is implemented.");
        }

        services
            .AddOptions<PersistenceOptions>()
            .Bind(configuration.GetSection(PersistenceOptions.SectionName));

        services
            .AddOptions<BrokerOptions>()
            .Bind(configuration.GetSection(BrokerOptions.SectionName));

        services.AddScoped<DapperContext>();
        services.AddScoped<IDbContext>(provider => provider.GetRequiredService<DapperContext>());
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<DapperContext>());
        services.AddScoped<IBrokerServerProfileRepository, BrokerServerProfileRepository>();
        services.AddScoped<IDiscoveredTopicRepository, DiscoveredTopicRepository>();
        services.AddScoped<IFavoriteNodeRepository, FavoriteNodeRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<INodeRepository, NodeRepository>();
        services.AddScoped<ISavedChannelFilterRepository, SavedChannelFilterRepository>();
        services.AddScoped<ISubscriptionIntentRepository, SubscriptionIntentRepository>();
        services.AddScoped<ITopicPresetRepository, TopicPresetRepository>();
        services.AddScoped<IUserAccountRepository, UserAccountRepository>();
        services.AddSingleton<IBrokerRuntimeCommandRepository, SqliteBrokerRuntimeCommandRepository>();
        services.AddSingleton<IBrokerRuntimeRegistry, SqliteBrokerRuntimeRegistry>();

        services.AddSingleton<SqliteDatabaseInitializer>();
        services.AddHostedService<PersistenceInitializationHostedService>();

        return services;
    }
}
