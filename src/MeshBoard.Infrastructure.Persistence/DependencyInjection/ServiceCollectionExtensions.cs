using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Infrastructure.Persistence.Context;
using MeshBoard.Infrastructure.Persistence.Hosted;
using MeshBoard.Infrastructure.Persistence.Initialization;
using MeshBoard.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace MeshBoard.Infrastructure.Persistence.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddProductPersistenceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        AddPersistenceOptions(services, configuration);
        var provider = GetProvider(configuration);

        EnsurePostgreSqlProvider(provider, "product");
        EnsureSharedPostgreSqlInfrastructure(services);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPersistenceInitializer, PostgresDatabaseInitializer>());
        RegisterProductRepositories(services);
        EnsurePersistenceInitializationHostedService(services);

        return services;
    }

    public static IServiceCollection AddCollectorPersistenceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        AddPersistenceOptions(services, configuration);
        var provider = GetProvider(configuration);
        EnsurePostgreSqlProvider(provider, "collector");
        EnsureSharedPostgreSqlInfrastructure(services);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPersistenceInitializer, PostgresDatabaseInitializer>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPersistenceInitializer, PostgresCollectorSchemaInitializer>());
        RegisterCollectorPersistenceRepositories(services);
        EnsurePersistenceInitializationHostedService(services);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, CollectorMessageRetentionHostedService>());

        return services;
    }

    public static IServiceCollection AddCollectorReadPersistenceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        AddPersistenceOptions(services, configuration);
        var provider = GetProvider(configuration);
        EnsurePostgreSqlProvider(provider, "collector read");
        EnsureSharedPostgreSqlInfrastructure(services);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPersistenceInitializer, PostgresCollectorSchemaInitializer>());
        RegisterCollectorReadRepositories(services);
        EnsurePersistenceInitializationHostedService(services);

        return services;
    }

    private static void AddPersistenceOptions(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<PersistenceOptions>()
            .Bind(configuration.GetSection(PersistenceOptions.SectionName));
    }

    private static string GetProvider(IConfiguration configuration)
    {
        return configuration.GetSection(PersistenceOptions.SectionName).GetValue<string>("Provider") ?? "PostgreSQL";
    }

    private static void RegisterProductRepositories(IServiceCollection services)
    {
        services.AddScoped<IBrokerServerProfileRepository, ProductBrokerServerProfileRepository>();
        services.AddScoped<IFavoriteNodeRepository, FavoriteNodeRepository>();
        services.AddScoped<IUserAccountRepository, UserAccountRepository>();
    }

    private static void RegisterCollectorPersistenceRepositories(IServiceCollection services)
    {
        services.AddScoped<CollectorChannelResolver>();
        services.AddScoped<IBrokerServerProfileRepository, ProductBrokerServerProfileRepository>();
        services.AddScoped<ICollectorPacketRollupRepository, CollectorPacketRollupRepository>();
        services.AddScoped<IDiscoveredTopicRepository, CollectorDiscoveredTopicRepository>();
        services.AddScoped<IMessageRepository, CollectorMessageRepository>();
        services.AddScoped<INeighborLinkRepository, CollectorNeighborLinkRepository>();
        services.AddScoped<INodeRepository, CollectorNodeRepository>();
    }

    private static void RegisterCollectorReadRepositories(IServiceCollection services)
    {
        services.AddScoped<ICollectorReadRepository, CollectorReadRepository>();
    }

    private static void EnsureSharedPostgreSqlInfrastructure(IServiceCollection services)
    {
        services.TryAddSingleton<IPersistenceConnectionFactory, PostgresPersistenceConnectionFactory>();
        services.TryAddScoped<DapperContext>();
        services.TryAddScoped<IDbContext>(provider => provider.GetRequiredService<DapperContext>());
        services.TryAddScoped<IUnitOfWork>(provider => provider.GetRequiredService<DapperContext>());
    }

    private static void EnsurePersistenceInitializationHostedService(IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, PersistenceInitializationHostedService>());
    }

    private static void EnsurePostgreSqlProvider(string provider, string surfaceName)
    {
        if (provider.Trim().ToLowerInvariant() is not ("postgres" or "postgresql"))
        {
            throw new NotSupportedException(
                $"The configured persistence provider '{provider}' is not supported for the {surfaceName} path. Only PostgreSQL is implemented.");
        }
    }
}
