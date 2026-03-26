namespace MeshBoard.Infrastructure.Persistence.SQL.Responses;

internal sealed class CollectorPacketTypeCountSqlResponse
{
    public required string PacketType { get; set; }

    public int PacketCount { get; set; }
}
