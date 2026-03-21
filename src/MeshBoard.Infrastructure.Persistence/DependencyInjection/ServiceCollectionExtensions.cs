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
    public static IServiceCollection AddProductPersistenceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        AddPersistenceOptions(services, configuration);
        RegisterProviderInfrastructure(services, GetProvider(configuration), includeLegacyRuntime: false);
        RegisterProductRepositories(services);

        services.AddHostedService<PersistenceInitializationHostedService>();

        return services;
    }

    public static IServiceCollection AddPersistenceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        AddPersistenceOptions(services, configuration);
        AddLegacyBrokerOptions(services, configuration);
        var provider = GetProvider(configuration);

        if (!string.Equals(provider, "SQLite", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                $"The configured persistence provider '{provider}' is not supported yet. Only SQLite is implemented.");
        }

        RegisterProviderInfrastructure(services, provider, includeLegacyRuntime: true);
        RegisterLegacyPersistenceRepositories(services);
        services.AddSingleton<IBrokerRuntimeCommandRepository, SqliteBrokerRuntimeCommandRepository>();
        services.AddSingleton<IBrokerRuntimeRegistry, SqliteBrokerRuntimeRegistry>();
        services.AddHostedService<PersistenceInitializationHostedService>();

        return services;
    }

    private static void AddPersistenceOptions(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<PersistenceOptions>()
            .Bind(configuration.GetSection(PersistenceOptions.SectionName));
    }

    private static void AddLegacyBrokerOptions(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<BrokerOptions>()
            .Bind(configuration.GetSection(BrokerOptions.SectionName));
    }

    private static string GetProvider(IConfiguration configuration)
    {
        return configuration.GetSection(PersistenceOptions.SectionName).GetValue<string>("Provider") ?? "SQLite";
    }

    private static void RegisterProviderInfrastructure(
        IServiceCollection services,
        string provider,
        bool includeLegacyRuntime)
    {
        switch (NormalizeProvider(provider))
        {
            case "sqlite":
                services.AddSingleton<IPersistenceConnectionFactory, SqlitePersistenceConnectionFactory>();
                services.AddSingleton<IPersistenceInitializer, SqliteDatabaseInitializer>();
                break;
            case "postgresql":
                if (includeLegacyRuntime)
                {
                    throw new NotSupportedException(
                        "Legacy runtime persistence remains SQLite-only. Use AddProductPersistenceInfrastructure for the API product path.");
                }

                services.AddSingleton<IPersistenceConnectionFactory, PostgresPersistenceConnectionFactory>();
                services.AddSingleton<IPersistenceInitializer, PostgresDatabaseInitializer>();
                break;
            default:
                throw new NotSupportedException(
                    $"The configured persistence provider '{provider}' is not supported.");
        }

        services.AddScoped<DapperContext>();
        services.AddScoped<IDbContext>(provider => provider.GetRequiredService<DapperContext>());
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<DapperContext>());
    }

    private static void RegisterProductRepositories(IServiceCollection services)
    {
        services.AddScoped<IBrokerServerProfileRepository, ProductBrokerServerProfileRepository>();
        services.AddScoped<IFavoriteNodeRepository, FavoriteNodeRepository>();
        services.AddScoped<IUserAccountRepository, UserAccountRepository>();
    }

    private static void RegisterLegacyPersistenceRepositories(IServiceCollection services)
    {
        services.AddScoped<IBrokerServerProfileRepository, BrokerServerProfileRepository>();
        services.AddScoped<IFavoriteNodeRepository, FavoriteNodeRepository>();
        services.AddScoped<IUserAccountRepository, UserAccountRepository>();
        services.AddScoped<IDiscoveredTopicRepository, DiscoveredTopicRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<INodeRepository, NodeRepository>();
        services.AddScoped<ISubscriptionIntentRepository, SubscriptionIntentRepository>();
    }

    private static string NormalizeProvider(string provider)
    {
        return provider.Trim().ToLowerInvariant() switch
        {
            "sqlite" => "sqlite",
            "postgres" => "postgresql",
            "postgresql" => "postgresql",
            _ => provider.Trim().ToLowerInvariant()
        };
    }
}
