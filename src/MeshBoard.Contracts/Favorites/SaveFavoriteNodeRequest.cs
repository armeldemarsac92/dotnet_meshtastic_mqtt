namespace MeshBoard.Contracts.Favorites;

public sealed class SaveFavoriteNodeRequest
{
    public required string NodeId { get; set; }

    public string? ShortName { get; set; }

    public string? LongName { get; set; }
}
