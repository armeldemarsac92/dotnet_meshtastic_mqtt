namespace MeshBoard.Api.SDK.Abstractions;

public interface IMeshBoardApiRequestConfigurator
{
    ValueTask ConfigureAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);
}
