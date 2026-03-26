using MeshBoard.Infrastructure.Neo4j.Configuration;
using MeshBoard.Infrastructure.Neo4j.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Neo4j.Driver;

namespace MeshBoard.Infrastructure.Neo4j.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNeo4jInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<Neo4jOptions>()
            .Bind(configuration.GetSection(Neo4jOptions.SectionName));

        services.AddSingleton<IDriver>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<Neo4jOptions>>().Value;
            return GraphDatabase.Driver(
                options.Uri,
                AuthTokens.Basic(options.Username, options.Password));
        });
        services.AddScoped<IGraphTopologyRepository, GraphTopologyRepository>();

        return services;
    }
}
