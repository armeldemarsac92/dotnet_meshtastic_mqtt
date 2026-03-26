using MeshBoard.Api.SDK.Abstractions;

namespace MeshBoard.RealtimeLoadTests.Api;

internal sealed class NoOpMeshBoardApiRequestConfigurator : IMeshBoardApiRequestConfigurator
{
    public ValueTask ConfigureAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }
}
