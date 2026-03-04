namespace MeshBoard.Contracts.Favorites;

public sealed class FavoriteNode
{
    public Guid Id { get; set; }

    public string NodeId { get; set; } = string.Empty;

    public string? ShortName { get; set; }

    public string? LongName { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
