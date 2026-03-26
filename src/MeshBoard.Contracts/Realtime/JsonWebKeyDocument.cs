namespace MeshBoard.Contracts.Realtime;

public sealed class JsonWebKeyDocument
{
    public List<JsonWebKey> Keys { get; set; } = [];
}
