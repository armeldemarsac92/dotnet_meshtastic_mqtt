namespace MeshBoard.Contracts.Messages;

public enum MessageVisibilityFilter
{
    All = 0,
    DecodedOnly = 1,
    OpaqueOnly = 2,
    PrivateOnly = 3
}
