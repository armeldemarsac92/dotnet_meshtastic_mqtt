using System.Globalization;
using MeshBoard.Contracts.Meshtastic;
using MeshBoard.Infrastructure.Persistence.SQL.Responses;

namespace MeshBoard.Infrastructure.Persistence.Mapping;

internal static class SubscriptionIntentMapping
{
    public static SubscriptionIntent MapToSubscriptionIntent(this SubscriptionIntentSqlResponse response)
    {
        return new SubscriptionIntent
        {
            BrokerServerProfileId = Guid.Parse(response.BrokerServerProfileId),
            TopicFilter = response.TopicFilter,
            CreatedAtUtc = ParseOrDefault(response.CreatedAtUtc)
        };
    }

    private static DateTimeOffset ParseOrDefault(string value)
    {
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsedValue)
            ? parsedValue
            : DateTimeOffset.UnixEpoch;
    }
}
