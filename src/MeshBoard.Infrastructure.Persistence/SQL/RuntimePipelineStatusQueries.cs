namespace MeshBoard.Infrastructure.Persistence.SQL;

internal static class RuntimePipelineStatusQueries
{
    public static string GetByWorkspaceId =>
        """
        SELECT
            workspace_id AS WorkspaceId,
            inbound_queue_capacity AS InboundQueueCapacity,
            inbound_worker_count AS InboundWorkerCount,
            inbound_queue_depth AS InboundQueueDepth,
            inbound_oldest_message_age_ms AS InboundOldestMessageAgeMilliseconds,
            inbound_enqueued_count AS InboundEnqueuedCount,
            inbound_dequeued_count AS InboundDequeuedCount,
            inbound_dropped_count AS InboundDroppedCount,
            updated_at_utc AS UpdatedAtUtc
        FROM runtime_pipeline_status
        WHERE workspace_id = @WorkspaceId
        LIMIT 1;
        """;

    public static string Upsert =>
        """
        INSERT INTO runtime_pipeline_status (
            workspace_id,
            inbound_queue_capacity,
            inbound_worker_count,
            inbound_queue_depth,
            inbound_oldest_message_age_ms,
            inbound_enqueued_count,
            inbound_dequeued_count,
            inbound_dropped_count,
            updated_at_utc
        )
        VALUES (
            @WorkspaceId,
            @InboundQueueCapacity,
            @InboundWorkerCount,
            @InboundQueueDepth,
            @InboundOldestMessageAgeMilliseconds,
            @InboundEnqueuedCount,
            @InboundDequeuedCount,
            @InboundDroppedCount,
            @UpdatedAtUtc
        )
        ON CONFLICT(workspace_id) DO UPDATE SET
            inbound_queue_capacity = excluded.inbound_queue_capacity,
            inbound_worker_count = excluded.inbound_worker_count,
            inbound_queue_depth = excluded.inbound_queue_depth,
            inbound_oldest_message_age_ms = excluded.inbound_oldest_message_age_ms,
            inbound_enqueued_count = excluded.inbound_enqueued_count,
            inbound_dequeued_count = excluded.inbound_dequeued_count,
            inbound_dropped_count = excluded.inbound_dropped_count,
            updated_at_utc = excluded.updated_at_utc;
        """;
}
