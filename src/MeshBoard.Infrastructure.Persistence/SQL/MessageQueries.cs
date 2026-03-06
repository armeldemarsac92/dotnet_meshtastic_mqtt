namespace MeshBoard.Infrastructure.Persistence.SQL;

internal static class MessageQueries
{
    public static string DeleteMessagesOlderThan =>
        """
        DELETE FROM message_history
        WHERE received_at_utc < @CutoffUtc;
        """;

    public static string GetRecentMessages =>
        """
        SELECT
            mh.id AS Id,
            COALESCE(mh.broker_server, 'unknown') AS BrokerServer,
            mh.topic AS Topic,
            mh.packet_type AS PacketType,
            mh.from_node_id AS FromNodeId,
            n.short_name AS FromNodeShortName,
            n.long_name AS FromNodeLongName,
            mh.to_node_id AS ToNodeId,
            mh.payload_preview AS PayloadPreview,
            mh.is_private AS IsPrivate,
            mh.received_at_utc AS ReceivedAtUtc
        FROM message_history mh
        LEFT JOIN nodes n ON n.node_id = mh.from_node_id
        ORDER BY mh.rowid DESC
        LIMIT @Take;
        """;

    public static string GetRecentMessagesByBroker =>
        """
        SELECT
            mh.id AS Id,
            COALESCE(mh.broker_server, 'unknown') AS BrokerServer,
            mh.topic AS Topic,
            mh.packet_type AS PacketType,
            mh.from_node_id AS FromNodeId,
            n.short_name AS FromNodeShortName,
            n.long_name AS FromNodeLongName,
            mh.to_node_id AS ToNodeId,
            mh.payload_preview AS PayloadPreview,
            mh.is_private AS IsPrivate,
            mh.received_at_utc AS ReceivedAtUtc
        FROM message_history mh
        LEFT JOIN nodes n ON n.node_id = mh.from_node_id
        WHERE COALESCE(mh.broker_server, 'unknown') = @BrokerServer
        ORDER BY mh.rowid DESC
        LIMIT @Take;
        """;

    public static string GetRecentMessagesBySender =>
        """
        SELECT
            mh.id AS Id,
            COALESCE(mh.broker_server, 'unknown') AS BrokerServer,
            mh.topic AS Topic,
            mh.packet_type AS PacketType,
            mh.from_node_id AS FromNodeId,
            n.short_name AS FromNodeShortName,
            n.long_name AS FromNodeLongName,
            mh.to_node_id AS ToNodeId,
            mh.payload_preview AS PayloadPreview,
            mh.is_private AS IsPrivate,
            mh.received_at_utc AS ReceivedAtUtc
        FROM message_history mh
        LEFT JOIN nodes n ON n.node_id = mh.from_node_id
        WHERE mh.from_node_id = @SenderNodeId
        ORDER BY mh.rowid DESC
        LIMIT @Take;
        """;

    public static string InsertMessage =>
        """
        INSERT INTO message_history (
            id,
            broker_server,
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
            COALESCE(NULLIF(@BrokerServer, ''), 'unknown'),
            @Topic,
            @PacketType,
            @MessageKey,
            @FromNodeId,
            @ToNodeId,
            @PayloadPreview,
            @IsPrivate,
            @ReceivedAtUtc)
        ON CONFLICT(message_key) DO UPDATE SET
            broker_server = excluded.broker_server,
            topic = excluded.topic,
            packet_type = excluded.packet_type,
            from_node_id = excluded.from_node_id,
            to_node_id = excluded.to_node_id,
            payload_preview = excluded.payload_preview,
            is_private = excluded.is_private,
            received_at_utc = excluded.received_at_utc
        WHERE message_history.packet_type IN ('Encrypted Packet', 'Unknown Packet', 'Legacy Packet')
          AND excluded.packet_type NOT IN ('Encrypted Packet', 'Unknown Packet', 'Legacy Packet');
        """;
}
