using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Infrastructure.Persistence.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MeshBoard.IntegrationTests;

public sealed class ProductPersistenceRegistrationTests
{
    [Fact]
    public void AddProductPersistenceInfrastructure_ShouldRegisterPostgreSqlFoundation()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration("PostgreSQL");
        var persistenceAssembly = typeof(ServiceCollectionExtensions).Assembly;
        var connectionFactoryServiceType = persistenceAssembly.GetType(
            "MeshBoard.Infrastructure.Persistence.Context.IPersistenceConnectionFactory",
            throwOnError: true)!;
        var initializerServiceType = persistenceAssembly.GetType(
            "MeshBoard.Infrastructure.Persistence.Initialization.IPersistenceInitializer",
            throwOnError: true)!;
        var expectedConnectionFactoryType = persistenceAssembly.GetType(
            "MeshBoard.Infrastructure.Persistence.Context.PostgresPersistenceConnectionFactory",
            throwOnError: true)!;
        var expectedInitializerType = persistenceAssembly.GetType(
            "MeshBoard.Infrastructure.Persistence.Initialization.PostgresDatabaseInitializer",
            throwOnError: true)!;

        services.AddProductPersistenceInfrastructure(configuration);

        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == connectionFactoryServiceType &&
                descriptor.ImplementationType == expectedConnectionFactoryType);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == initializerServiceType &&
                descriptor.ImplementationType == expectedInitializerType);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType?.Name == "PersistenceInitializationHostedService");
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IBrokerServerProfileRepository) &&
                descriptor.ImplementationType?.Name == "ProductBrokerServerProfileRepository");
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IMessageRepository));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(INodeRepository));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IDiscoveredTopicRepository));
    }

    [Fact]
    public void AddCollectorPersistenceInfrastructure_ShouldRegisterPostgreSqlCollectorFoundation()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration("PostgreSQL");
        var persistenceAssembly = typeof(ServiceCollectionExtensions).Assembly;
        var connectionFactoryServiceType = persistenceAssembly.GetType(
            "MeshBoard.Infrastructure.Persistence.Context.IPersistenceConnectionFactory",
            throwOnError: true)!;
        var initializerServiceType = persistenceAssembly.GetType(
            "MeshBoard.Infrastructure.Persistence.Initialization.IPersistenceInitializer",
            throwOnError: true)!;
        var expectedConnectionFactoryType = persistenceAssembly.GetType(
            "MeshBoard.Infrastructure.Persistence.Context.PostgresPersistenceConnectionFactory",
            throwOnError: true)!;
        var productInitializerType = persistenceAssembly.GetType(
            "MeshBoard.Infrastructure.Persistence.Initialization.PostgresDatabaseInitializer",
            throwOnError: true)!;
        var collectorInitializerType = persistenceAssembly.GetType(
            "MeshBoard.Infrastructure.Persistence.Initialization.PostgresCollectorSchemaInitializer",
            throwOnError: true)!;

        services.AddCollectorPersistenceInfrastructure(configuration);

        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == connectionFactoryServiceType &&
                descriptor.ImplementationType == expectedConnectionFactoryType);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == initializerServiceType &&
                descriptor.ImplementationType == productInitializerType);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == initializerServiceType &&
                descriptor.ImplementationType == collectorInitializerType);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType?.Name == "PersistenceInitializationHostedService");
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IBrokerServerProfileRepository) &&
                descriptor.ImplementationType?.Name == "ProductBrokerServerProfileRepository");
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IMessageRepository) &&
                descriptor.ImplementationType?.Name == "CollectorMessageRepository");
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(ICollectorPacketRollupRepository) &&
                descriptor.ImplementationType?.Name == "CollectorPacketRollupRepository");
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(INodeRepository) &&
                descriptor.ImplementationType?.Name == "CollectorNodeRepository");
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IDiscoveredTopicRepository) &&
                descriptor.ImplementationType?.Name == "CollectorDiscoveredTopicRepository");
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IFavoriteNodeRepository));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IUserAccountRepository));
    }

    [Fact]
    public void AddCollectorReadPersistenceInfrastructure_ShouldRegisterCollectorReadFoundation()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration("PostgreSQL");
        var persistenceAssembly = typeof(ServiceCollectionExtensions).Assembly;
        var connectionFactoryServiceType = persistenceAssembly.GetType(
            "MeshBoard.Infrastructure.Persistence.Context.IPersistenceConnectionFactory",
            throwOnError: true)!;
        var initializerServiceType = persistenceAssembly.GetType(
            "MeshBoard.Infrastructure.Persistence.Initialization.IPersistenceInitializer",
            throwOnError: true)!;
        var expectedConnectionFactoryType = persistenceAssembly.GetType(
            "MeshBoard.Infrastructure.Persistence.Context.PostgresPersistenceConnectionFactory",
            throwOnError: true)!;
        var expectedInitializerType = persistenceAssembly.GetType(
            "MeshBoard.Infrastructure.Persistence.Initialization.PostgresCollectorSchemaInitializer",
            throwOnError: true)!;

        services.AddCollectorReadPersistenceInfrastructure(configuration);

        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == connectionFactoryServiceType &&
                descriptor.ImplementationType == expectedConnectionFactoryType);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == initializerServiceType &&
                descriptor.ImplementationType == expectedInitializerType);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType?.Name == "PersistenceInitializationHostedService");
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(ICollectorReadRepository) &&
                descriptor.ImplementationType?.Name == "CollectorReadRepository");
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(ICollectorPacketRollupRepository));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IMessageRepository));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(INodeRepository));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IDiscoveredTopicRepository));
    }

    [Fact]
    public void AddProductPersistenceInfrastructure_ShouldRejectSqliteProvider()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration("SQLite");

        var exception = Assert.Throws<NotSupportedException>(() => services.AddProductPersistenceInfrastructure(configuration));
        Assert.Contains("Only PostgreSQL is implemented", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddCollectorPersistenceInfrastructure_ShouldRejectSqliteProvider()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration("SQLite");

        var exception = Assert.Throws<NotSupportedException>(() => services.AddCollectorPersistenceInfrastructure(configuration));
        Assert.Contains("Only PostgreSQL is implemented", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddCollectorReadPersistenceInfrastructure_ShouldRejectSqliteProvider()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration("SQLite");

        var exception = Assert.Throws<NotSupportedException>(() => services.AddCollectorReadPersistenceInfrastructure(configuration));
        Assert.Contains("Only PostgreSQL is implemented", exception.Message, StringComparison.Ordinal);
    }

    private static IConfiguration CreateConfiguration(string provider)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [$"{PersistenceOptions.SectionName}:Provider"] = provider,
                    [$"{PersistenceOptions.SectionName}:ConnectionString"] = "Host=localhost;Database=meshboard;Username=meshboard;Password=meshboard"
                })
            .Build();
    }
}
