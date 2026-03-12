namespace MeshBoard.Contracts.Authentication;

public sealed class AntiforgeryTokenResponse
{
    public required string RequestToken { get; set; }
}
