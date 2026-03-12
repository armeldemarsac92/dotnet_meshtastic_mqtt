using MeshBoard.Api.SDK.Abstractions;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace MeshBoard.Client.Services;

public sealed class BrowserMeshBoardApiRequestConfigurator : IMeshBoardApiRequestConfigurator
{
    public ValueTask ConfigureAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return ValueTask.CompletedTask;
    }
}
