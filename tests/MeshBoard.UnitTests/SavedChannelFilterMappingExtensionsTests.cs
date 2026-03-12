using MeshBoard.Contracts.Meshtastic;
using MeshBoard.Contracts.Topics;

namespace MeshBoard.UnitTests;

public sealed class SavedChannelFilterMappingExtensionsTests
{
    [Fact]
    public void ToSavedChannelFilter_ShouldProjectSafeChannelFields()
    {
        var subscriptionIntent = new SubscriptionIntent
        {
            BrokerServerProfileId = Guid.NewGuid(),
            TopicFilter = "msh/US/2/e/LongFast/#",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var savedChannelFilter = subscriptionIntent.ToSavedChannelFilter();

        Assert.Equal(subscriptionIntent.BrokerServerProfileId, savedChannelFilter.BrokerServerProfileId);
        Assert.Equal(subscriptionIntent.TopicFilter, savedChannelFilter.TopicFilter);
        Assert.Equal(subscriptionIntent.CreatedAtUtc, savedChannelFilter.CreatedAtUtc);
    }
}
