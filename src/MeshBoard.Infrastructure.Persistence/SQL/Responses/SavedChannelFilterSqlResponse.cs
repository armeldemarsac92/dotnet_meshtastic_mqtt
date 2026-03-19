namespace MeshBoard.Infrastructure.Persistence.SQL.Responses;

internal sealed class SavedChannelFilterSqlResponse
{
    public required string Id { get; set; }

    public required string BrokerServerProfileId { get; set; }

    public required string TopicFilter { get; set; }

    public string? Label { get; set; }

    public required string CreatedAtUtc { get; set; }

    public required string UpdatedAtUtc { get; set; }
}
