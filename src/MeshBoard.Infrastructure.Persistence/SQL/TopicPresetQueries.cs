namespace MeshBoard.Infrastructure.Persistence.SQL;

internal static class TopicPresetQueries
{
    public static string ClearDefaultTopicPresets =>
        """
        UPDATE topic_presets
        SET is_default = 0
        WHERE is_default = 1;
        """;

    public static string GetTopicPresetByTopicPattern =>
        """
        SELECT
            id AS Id,
            name AS Name,
            topic_pattern AS TopicPattern,
            is_default AS IsDefault,
            created_at_utc AS CreatedAtUtc
        FROM topic_presets
        WHERE topic_pattern = @TopicPattern;
        """;

    public static string GetTopicPresets =>
        """
        SELECT
            id AS Id,
            name AS Name,
            topic_pattern AS TopicPattern,
            is_default AS IsDefault,
            created_at_utc AS CreatedAtUtc
        FROM topic_presets
        ORDER BY is_default DESC, name ASC;
        """;

    public static string InsertTopicPresetIfMissing =>
        """
        INSERT OR IGNORE INTO topic_presets (
            id,
            name,
            topic_pattern,
            is_default,
            created_at_utc)
        VALUES (
            @Id,
            @Name,
            @TopicPattern,
            @IsDefault,
            @CreatedAtUtc);
        """;

    public static string UpsertTopicPreset =>
        """
        INSERT INTO topic_presets (
            id,
            name,
            topic_pattern,
            is_default,
            created_at_utc)
        VALUES (
            @Id,
            @Name,
            @TopicPattern,
            @IsDefault,
            @CreatedAtUtc)
        ON CONFLICT(topic_pattern) DO UPDATE SET
            name = excluded.name,
            is_default = excluded.is_default;
        """;
}
