namespace MeshBoard.Infrastructure.Persistence.SQL.Responses;

internal sealed class SubscriptionIntentSqlResponse
{
    public string BrokerServerProfileId { get; set; } = string.Empty;

    public string TopicFilter { get; set; } = string.Empty;

    public string CreatedAtUtc { get; set; } = string.Empty;
}
