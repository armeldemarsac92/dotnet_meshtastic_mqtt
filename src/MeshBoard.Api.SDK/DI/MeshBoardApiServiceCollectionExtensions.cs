using System.Text.Json;
using MeshBoard.Api.SDK.API;
using MeshBoard.Api.SDK.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace MeshBoard.Api.SDK.DI;

public static class MeshBoardApiServiceCollectionExtensions
{
    public static IServiceCollection AddMeshBoardApiSdk(this IServiceCollection services, Uri baseAddress)
    {
        services.AddTransient<MeshBoardApiRequestConfigurationHandler>();
        services.AddTransient<AntiforgeryTokenHeaderHandler>();

        RegisterRefitClient<IAntiforgeryApi>(services, baseAddress, includeAntiforgery: false);
        RegisterRefitClient<IAuthApi>(services, baseAddress, includeAntiforgery: true);
        RegisterRefitClient<IFavoritePreferenceApi>(services, baseAddress, includeAntiforgery: true);
        RegisterRefitClient<IBrokerPreferenceApi>(services, baseAddress, includeAntiforgery: false);
        RegisterRefitClient<ITopicPresetPreferenceApi>(services, baseAddress, includeAntiforgery: false);

        return services;
    }

    private static void RegisterRefitClient<TClient>(IServiceCollection services, Uri baseAddress, bool includeAntiforgery)
        where TClient : class
    {
        var builder = services
            .AddRefitClient<TClient>(CreateRefitSettings())
            .ConfigureHttpClient(client => client.BaseAddress = baseAddress)
            .AddHttpMessageHandler<MeshBoardApiRequestConfigurationHandler>();

        if (includeAntiforgery)
        {
            builder.AddHttpMessageHandler<AntiforgeryTokenHeaderHandler>();
        }
    }

    private static RefitSettings CreateRefitSettings()
    {
        return new RefitSettings
        {
            ContentSerializer = new SystemTextJsonContentSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web))
        };
    }
}
