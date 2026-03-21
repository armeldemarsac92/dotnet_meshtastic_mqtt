using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Infrastructure.Persistence.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MeshBoard.IntegrationTests;

public sealed class ProductPersistenceRegistrationTests
{
    [Theory]
    [InlineData(
        "SQLite",
        "MeshBoard.Infrastructure.Persistence.Context.SqlitePersistenceConnectionFactory",
        "MeshBoard.Infrastructure.Persistence.Initialization.SqliteDatabaseInitializer")]
    [InlineData(
        "PostgreSQL",
        "MeshBoard.Infrastructure.Persistence.Context.PostgresPersistenceConnectionFactory",
        "MeshBoard.Infrastructure.Persistence.Initialization.PostgresDatabaseInitializer")]
    public void AddProductPersistenceInfrastructure_ShouldRegisterProviderSpecificFoundation(
        string provider,
        string expectedConnectionFactoryTypeName,
        string expectedInitializerTypeName)
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(provider);
        var persistenceAssembly = typeof(ServiceCollectionExtensions).Assembly;
        var connectionFactoryServiceType = persistenceAssembly.GetType(
            "MeshBoard.Infrastructure.Persistence.Context.IPersistenceConnectionFactory",
            throwOnError: true)!;
        var initializerServiceType = persistenceAssembly.GetType(
            "MeshBoard.Infrastructure.Persistence.Initialization.IPersistenceInitializer",
            throwOnError: true)!;
        var expectedConnectionFactoryType = persistenceAssembly.GetType(expectedConnectionFactoryTypeName, throwOnError: true)!;
        var expectedInitializerType = persistenceAssembly.GetType(expectedInitializerTypeName, throwOnError: true)!;

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
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(ISubscriptionIntentRepository));
    }

    [Fact]
    public void AddPersistenceInfrastructure_ShouldRejectPostgreSqlProvider()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration("PostgreSQL");

        var exception = Assert.Throws<NotSupportedException>(() => services.AddPersistenceInfrastructure(configuration));
        Assert.Contains("Only SQLite is implemented", exception.Message, StringComparison.Ordinal);
    }

    private static IConfiguration CreateConfiguration(string provider)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [$"{PersistenceOptions.SectionName}:Provider"] = provider,
                    [$"{PersistenceOptions.SectionName}:ConnectionString"] =
                        provider.Equals("SQLite", StringComparison.OrdinalIgnoreCase)
                            ? "Data Source=/tmp/meshboard-product-tests.db"
                            : "Host=localhost;Database=meshboard;Username=meshboard;Password=meshboard"
                })
            .Build();
    }
}
