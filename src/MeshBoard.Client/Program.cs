using MeshBoard.Client.Authentication;
using MeshBoard.Client.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Refit;
using System.Text.Json;

namespace MeshBoard.Client;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);
        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");

        var apiBaseAddress = new Uri(builder.HostEnvironment.BaseAddress);

        builder.Services.AddAuthorizationCore();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddScoped<AuthSessionState>();
        builder.Services.AddScoped<AuthenticationStateProvider, ApiAuthenticationStateProvider>();
        builder.Services.AddTransient<BrowserRequestCredentialsHandler>();
        builder.Services.AddTransient<AntiforgeryTokenHeaderHandler>();
        builder.Services.AddScoped<AntiforgeryTokenProvider>();
        builder.Services.AddScoped<AuthApiClient>();
        builder.Services.AddScoped<BrokerPreferenceApiClient>();
        builder.Services.AddScoped<FavoritePreferenceApiClient>();
        builder.Services.AddScoped<TopicPresetPreferenceApiClient>();

        builder.Services
            .AddRefitClient<IAntiforgeryApi>(CreateRefitSettings())
            .ConfigureHttpClient(client => client.BaseAddress = apiBaseAddress)
            .AddHttpMessageHandler<BrowserRequestCredentialsHandler>();

        builder.Services
            .AddRefitClient<IAuthApi>(CreateRefitSettings())
            .ConfigureHttpClient(client => client.BaseAddress = apiBaseAddress)
            .AddHttpMessageHandler<BrowserRequestCredentialsHandler>()
            .AddHttpMessageHandler<AntiforgeryTokenHeaderHandler>();

        builder.Services
            .AddRefitClient<IFavoritePreferenceApi>(CreateRefitSettings())
            .ConfigureHttpClient(client => client.BaseAddress = apiBaseAddress)
            .AddHttpMessageHandler<BrowserRequestCredentialsHandler>()
            .AddHttpMessageHandler<AntiforgeryTokenHeaderHandler>();

        builder.Services
            .AddRefitClient<IBrokerPreferenceApi>(CreateRefitSettings())
            .ConfigureHttpClient(client => client.BaseAddress = apiBaseAddress)
            .AddHttpMessageHandler<BrowserRequestCredentialsHandler>();

        builder.Services
            .AddRefitClient<ITopicPresetPreferenceApi>(CreateRefitSettings())
            .ConfigureHttpClient(client => client.BaseAddress = apiBaseAddress)
            .AddHttpMessageHandler<BrowserRequestCredentialsHandler>();

        await builder.Build().RunAsync();
    }

    private static RefitSettings CreateRefitSettings()
    {
        return new RefitSettings
        {
            ContentSerializer = new SystemTextJsonContentSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web))
        };
    }
}
