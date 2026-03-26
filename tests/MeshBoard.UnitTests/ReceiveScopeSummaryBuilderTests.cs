using MeshBoard.Client.Messages;
using MeshBoard.Contracts.Configuration;

namespace MeshBoard.UnitTests;

public sealed class ReceiveScopeSummaryBuilderTests
{
    private readonly ReceiveScopeSummaryBuilder _builder = new();

    [Fact]
    public void Build_ShouldReturnEmptySummary_WhenActiveServerIsMissing()
    {
        var summary = _builder.Build(null);

        Assert.False(summary.HasActiveServer);
        Assert.Empty(summary.Topics);
    }

    [Fact]
    public void Build_ShouldReturnDefaultTopic_WhenActiveServerIsPresent()
    {
        var activeServer = CreateServerProfile(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "Public US",
            "mqtt.meshtastic.org:1883");

        var summary = _builder.Build(activeServer);

        Assert.True(summary.HasActiveServer);
        Assert.Equal(activeServer.Name, summary.ServerName);
        Assert.False(summary.UsesFallbackTopic);
        var topic = Assert.Single(summary.Topics);
        Assert.Equal("msh/#", topic.TopicPattern);
        Assert.Equal("Default", topic.SourceLabel);
        Assert.False(topic.IsFallback);
    }

    private static SavedBrokerServerProfile CreateServerProfile(
        Guid id,
        string name,
        string serverAddress)
    {
        return new SavedBrokerServerProfile
        {
            Id = id,
            Name = name,
            Host = serverAddress.Split(':')[0],
            Port = int.Parse(serverAddress.Split(':')[1]),
            ServerAddress = serverAddress
        };
    }
}
