using MeshBoard.Contracts.Meshtastic;

namespace MeshBoard.Contracts.Topics;

public static class SavedChannelFilterMappingExtensions
{
    public static SavedChannelFilter ToSavedChannelFilter(this SubscriptionIntent subscriptionIntent)
    {
        ArgumentNullException.ThrowIfNull(subscriptionIntent);

        return new SavedChannelFilter
        {
            BrokerServerProfileId = subscriptionIntent.BrokerServerProfileId,
            TopicFilter = subscriptionIntent.TopicFilter,
            CreatedAtUtc = subscriptionIntent.CreatedAtUtc
        };
    }
}
