using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Infrastructure.Meshtastic.DependencyInjection;
using MeshBoard.Infrastructure.Meshtastic.Hosted;
using MeshBoard.Infrastructure.Meshtastic.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MeshBoard.UnitTests;

public sealed class MeshtasticRuntimeRegistrationTests
{
    [Fact]
    public void AddMeshtasticRuntimeInfrastructure_ShouldUseDirectRuntimeCommandService()
    {
        var services = new ServiceCollection();

        services.AddMeshtasticRuntimeInfrastructure(CreateHostedRuntimeConfiguration());

        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(LocalBrokerRuntimeCommandService) &&
                descriptor.ImplementationType == typeof(LocalBrokerRuntimeCommandService));
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IBrokerRuntimeCommandExecutor) &&
                descriptor.ImplementationFactory is not null);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IBrokerRuntimeCommandService) &&
                descriptor.ImplementationFactory is not null);
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(IBrokerRuntimeCommandService) &&
                descriptor.ImplementationType == typeof(QueuedBrokerRuntimeCommandService));
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType == typeof(BrokerRuntimeCommandProcessorHostedService));
    }

    [Fact]
    public void AddMeshtasticInfrastructure_ShouldKeepQueuedRuntimeCommandService()
    {
        var services = new ServiceCollection();

        services.AddMeshtasticInfrastructure(CreateHostedRuntimeConfiguration());

        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IBrokerRuntimeCommandExecutor) &&
                descriptor.ImplementationType == typeof(LocalBrokerRuntimeCommandService));
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IBrokerRuntimeCommandService) &&
                descriptor.ImplementationType == typeof(QueuedBrokerRuntimeCommandService));
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType == typeof(BrokerRuntimeCommandProcessorHostedService));
    }

    private static IConfiguration CreateHostedRuntimeConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["MeshtasticRuntime:EnableHostedService"] = "true"
                })
            .Build();
    }
}
