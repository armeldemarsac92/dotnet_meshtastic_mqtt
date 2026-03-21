using System.Text.Json;
using MeshBoard.Api.SDK.API;
using MeshBoard.Api.SDK.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace MeshBoard.Api.SDK.DI;

public static class MeshBoardApiServiceCollectionExtensions
{
    public static IServiceCollection AddMeshBoardApiSdk(
        this IServiceCollection services,
        Uri baseAddress,
        Action<HttpClientHandler>? configurePrimaryHandler = null)
    {
        services.AddTransient<MeshBoardApiRequestConfigurationHandler>();
        services.AddTransient<AntiforgeryTokenHeaderHandler>();

        RegisterRefitClient<IAntiforgeryApi>(services, baseAddress, includeAntiforgery: false, configurePrimaryHandler);
        RegisterRefitClient<IAuthApi>(services, baseAddress, includeAntiforgery: true, configurePrimaryHandler);
        RegisterRefitClient<IFavoritePreferenceApi>(services, baseAddress, includeAntiforgery: true, configurePrimaryHandler);
        RegisterRefitClient<IBrokerPreferenceApi>(services, baseAddress, includeAntiforgery: true, configurePrimaryHandler);
        RegisterRefitClient<IRealtimeSessionApi>(services, baseAddress, includeAntiforgery: true, configurePrimaryHandler);
        RegisterRefitClient<IPublicCollectorApi>(services, baseAddress, includeAntiforgery: false, configurePrimaryHandler);

        return services;
    }

    private static void RegisterRefitClient<TClient>(
        IServiceCollection services,
        Uri baseAddress,
        bool includeAntiforgery,
        Action<HttpClientHandler>? configurePrimaryHandler)
        where TClient : class
    {
        var builder = services
            .AddRefitClient<TClient>(CreateRefitSettings())
            .ConfigureHttpClient(client => client.BaseAddress = baseAddress)
            .AddHttpMessageHandler<MeshBoardApiRequestConfigurationHandler>();

        if (configurePrimaryHandler is not null)
        {
            builder.ConfigurePrimaryHttpMessageHandler(
                () =>
                {
                    var handler = new HttpClientHandler();
                    configurePrimaryHandler(handler);
                    return handler;
                });
        }

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
