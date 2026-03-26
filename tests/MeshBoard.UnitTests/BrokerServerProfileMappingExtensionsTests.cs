using MeshBoard.Contracts.Configuration;

namespace MeshBoard.UnitTests;

public sealed class BrokerServerProfileMappingExtensionsTests
{
    [Fact]
    public void ToSavedBrokerServerProfile_ShouldMaskPasswordValue()
    {
        var profile = new BrokerServerProfile
        {
            Id = Guid.NewGuid(),
            Name = "Primary",
            Host = "mqtt.example.com",
            Port = 8883,
            UseTls = true,
            Username = "mesh-user",
            Password = "secret",
            DownlinkTopic = "msh/EU/2/json/mqtt/",
            EnableSend = true,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var saved = profile.ToSavedBrokerServerProfile();

        Assert.True(saved.HasPasswordConfigured);
        Assert.Equal(profile.ServerAddress, saved.ServerAddress);
        Assert.Equal(profile.DownlinkTopic, saved.DownlinkTopic);
    }

    [Fact]
    public void ToSavedBrokerServerProfile_ShouldReportNoPassword_WhenEmpty()
    {
        var profile = new BrokerServerProfile
        {
            Id = Guid.NewGuid(),
            Name = "Primary",
            Host = "mqtt.example.com",
            Port = 1883,
            Username = "mesh-user",
            Password = string.Empty
        };

        var saved = profile.ToSavedBrokerServerProfile();

        Assert.False(saved.HasPasswordConfigured);
    }
}
