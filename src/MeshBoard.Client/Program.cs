using MeshBoard.Api.SDK.Abstractions;
using MeshBoard.Api.SDK.DI;
using MeshBoard.Client.Authentication;
using MeshBoard.Client.Services;
using MeshBoard.Client.Vault;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

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
        builder.Services.AddScoped<IMeshBoardApiRequestConfigurator, BrowserMeshBoardApiRequestConfigurator>();
        builder.Services.AddScoped<AntiforgeryTokenProvider>();
        builder.Services.AddScoped<IAntiforgeryRequestTokenProvider>(sp => sp.GetRequiredService<AntiforgeryTokenProvider>());
        builder.Services.AddScoped<AuthApiClient>();
        builder.Services.AddScoped<BrowserVaultStore>();
        builder.Services.AddScoped<BrokerPreferenceApiClient>();
        builder.Services.AddScoped<ChannelPreferenceApiClient>();
        builder.Services.AddScoped<FavoritePreferenceApiClient>();
        builder.Services.AddScoped<LocalVaultService>();
        builder.Services.AddScoped<RealtimeSessionApiClient>();
        builder.Services.AddScoped<TopicPresetPreferenceApiClient>();
        builder.Services.AddScoped<VaultSessionState>();
        builder.Services.AddMeshBoardApiSdk(apiBaseAddress);

        await builder.Build().RunAsync();
    }
}
