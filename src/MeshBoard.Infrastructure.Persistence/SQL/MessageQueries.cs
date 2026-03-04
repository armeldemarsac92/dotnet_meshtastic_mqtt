namespace MeshBoard.Infrastructure.Persistence.SQL;

internal static class MessageQueries
{
    public static string GetRecentMessages =>
        """
        SELECT
            id AS Id,
            topic AS Topic,
            packet_type AS PacketType,
            from_node_id AS FromNodeId,
            to_node_id AS ToNodeId,
            payload_preview AS PayloadPreview,
            is_private AS IsPrivate,
            received_at_utc AS ReceivedAtUtc
        FROM message_history
        ORDER BY received_at_utc DESC
        LIMIT @Take;
        """;

    public static string InsertMessage =>
        """
        INSERT OR IGNORE INTO message_history (
            id,
            topic,
            packet_type,
            message_key,
            from_node_id,
            to_node_id,
            payload_preview,
            is_private,
            received_at_utc)
        VALUES (
            @Id,
            @Topic,
            @PacketType,
            @MessageKey,
            @FromNodeId,
            @ToNodeId,
            @PayloadPreview,
            @IsPrivate,
            @ReceivedAtUtc);
        """;
}
