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
    public void AddMeshtasticRuntimeInfrastructure_ShouldUseDirectRuntimeService()
    {
        var services = new ServiceCollection();

        services.AddMeshtasticRuntimeInfrastructure(CreateHostedRuntimeConfiguration());

        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(LocalBrokerRuntimeService) &&
                descriptor.ImplementationType == typeof(LocalBrokerRuntimeService));
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IBrokerRuntimeService) &&
                descriptor.ImplementationFactory is not null);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType == typeof(ActiveWorkspaceRuntimeReconcileHostedService));
    }

    [Fact]
    public void AddMeshtasticCollectorInfrastructure_ShouldUseDirectRuntimeAndIngestion()
    {
        var services = new ServiceCollection();

        services.AddMeshtasticCollectorInfrastructure(CreateHostedRuntimeConfiguration());

        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(LocalBrokerRuntimeService) &&
                descriptor.ImplementationType == typeof(LocalBrokerRuntimeService));
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IBrokerRuntimeService) &&
                descriptor.ImplementationFactory is not null);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType == typeof(ActiveWorkspaceRuntimeReconcileHostedService));
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType == typeof(MeshtasticInboundProcessingHostedService));
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
