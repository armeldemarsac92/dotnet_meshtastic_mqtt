namespace MeshBoard.Infrastructure.Persistence.SQL;

internal static class MessageQueries
{
    public static string GetRecentMessages =>
        """
        SELECT
            id AS Id,
            topic AS Topic,
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
        INSERT INTO message_history (
            id,
            topic,
            from_node_id,
            to_node_id,
            payload_preview,
            is_private,
            received_at_utc)
        VALUES (
            @Id,
            @Topic,
            @FromNodeId,
            @ToNodeId,
            @PayloadPreview,
            @IsPrivate,
            @ReceivedAtUtc);
        """;
}
