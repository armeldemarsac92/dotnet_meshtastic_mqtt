using MeshBoard.Infrastructure.Meshtastic.Mqtt;

namespace MeshBoard.UnitTests;

public sealed class MqttSessionTests
{
    [Fact]
    public void CreateClientId_ShouldBeStable_ForSameWorkspaceAndProfile()
    {
        var connectionSettings = new MqttSessionConnectionSettings
        {
            WorkspaceId = "workspace-a",
            BrokerServerProfileId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Host = "mqtt.meshtastic.org",
            Port = 1883
        };

        var firstClientId = MqttSession.CreateClientId("meshboard-worker", connectionSettings);
        var secondClientId = MqttSession.CreateClientId("meshboard-worker", connectionSettings);

        Assert.Equal(firstClientId, secondClientId);
        Assert.StartsWith("meshboard-worker-", firstClientId, StringComparison.Ordinal);
        Assert.True(firstClientId.Length <= 64);
    }

    [Fact]
    public void CreateClientId_ShouldBeDistinct_PerWorkspaceProfilePair()
    {
        var firstConnectionSettings = new MqttSessionConnectionSettings
        {
            WorkspaceId = "workspace-a",
            BrokerServerProfileId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Host = "mqtt.meshtastic.org",
            Port = 1883
        };
        var secondConnectionSettings = new MqttSessionConnectionSettings
        {
            WorkspaceId = "workspace-b",
            BrokerServerProfileId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Host = "mqtt.meshtastic.org",
            Port = 1883
        };

        var firstClientId = MqttSession.CreateClientId("meshboard-worker", firstConnectionSettings);
        var secondClientId = MqttSession.CreateClientId("meshboard-worker", secondConnectionSettings);

        Assert.NotEqual(firstClientId, secondClientId);
    }

    [Fact]
    public void CreateClientId_ShouldNormalizeBaseValue_WhenConfiguredValueContainsWhitespaceAndSymbols()
    {
        var connectionSettings = new MqttSessionConnectionSettings
        {
            WorkspaceId = "workspace-a",
            BrokerServerProfileId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Host = "mqtt.meshtastic.org",
            Port = 1883
        };

        var clientId = MqttSession.CreateClientId("  MeshBoard Worker / EU  ", connectionSettings);

        Assert.StartsWith("meshboard-worker---eu-", clientId, StringComparison.Ordinal);
        Assert.True(clientId.Length <= 64);
    }
}
