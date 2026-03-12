namespace MeshBoard.Api.SDK.Abstractions;

public interface IAntiforgeryRequestTokenProvider
{
    Task<string> GetAsync(bool forceRefresh = false, CancellationToken cancellationToken = default);
}
