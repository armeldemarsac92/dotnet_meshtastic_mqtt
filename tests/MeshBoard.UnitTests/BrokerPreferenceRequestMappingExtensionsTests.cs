using MeshBoard.Contracts.Configuration;
namespace MeshBoard.UnitTests;

public sealed class BrokerPreferenceRequestMappingExtensionsTests
{
    [Fact]
    public void ToSaveBrokerServerProfileRequest_ShouldUseNullKeyAndEmptyPassword_ForNewProfiles()
    {
        var request = new SaveBrokerPreferenceRequest
        {
            Name = "Primary",
            Host = "mqtt.example.com"
        };

        var mapped = request.ToSaveBrokerServerProfileRequest();

        Assert.Null(mapped.Id);
        Assert.Equal(string.Empty, mapped.Password);
        Assert.False(mapped.IsActive);
    }

    [Fact]
    public void ToSaveBrokerServerProfileRequest_ShouldClearServerOwnedKey_WhenPasswordIsOmitted()
    {
        var existing = new BrokerServerProfile
        {
            Id = Guid.NewGuid(),
            Name = "Primary",
            Host = "mqtt.example.com",
            Port = 8883,
            Password = "kept-secret",
            IsActive = true
        };

        var request = new SaveBrokerPreferenceRequest
        {
            Name = "Primary v2",
            Host = "mqtt.example.com",
            Port = 8883
        };

        var mapped = request.ToSaveBrokerServerProfileRequest(existing);

        Assert.Equal(existing.Id, mapped.Id);
        Assert.Equal(existing.Password, mapped.Password);
        Assert.True(mapped.IsActive);
    }

    [Fact]
    public void ToSaveBrokerServerProfileRequest_ShouldClearPassword_WhenRequested()
    {
        var existing = new BrokerServerProfile
        {
            Id = Guid.NewGuid(),
            Name = "Primary",
            Host = "mqtt.example.com",
            Password = "kept-secret"
        };

        var request = new SaveBrokerPreferenceRequest
        {
            Name = "Primary",
            Host = "mqtt.example.com",
            ClearPassword = true
        };

        var mapped = request.ToSaveBrokerServerProfileRequest(existing);

        Assert.Equal(string.Empty, mapped.Password);
    }
}
