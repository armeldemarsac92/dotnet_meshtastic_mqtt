using MeshBoard.Client.Messages;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Topics;

namespace MeshBoard.UnitTests;

public sealed class ReceiveScopeSummaryBuilderTests
{
    private readonly ReceiveScopeSummaryBuilder _builder = new();

    [Fact]
    public void Build_ShouldReturnEmptySummary_WhenActiveServerIsMissing()
    {
        var summary = _builder.Build(null, [], []);

        Assert.False(summary.HasActiveServer);
        Assert.Empty(summary.Topics);
    }

    [Fact]
    public void Build_ShouldUseOnlyTopicsFromTheActiveServer()
    {
        var activeServer = CreateServerProfile(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "Public US",
            "mqtt.meshtastic.org:1883",
            "msh/US/2/e/#");

        var otherServerId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var summary = _builder.Build(
            activeServer,
            [
                new SavedTopicPreset
                {
                    Id = Guid.NewGuid(),
                    ServerProfileId = activeServer.Id,
                    ServerProfileName = activeServer.Name,
                    ServerAddress = activeServer.ServerAddress,
                    TopicPattern = "msh/US/2/e/#",
                    IsDefault = true
                },
                new SavedTopicPreset
                {
                    Id = Guid.NewGuid(),
                    ServerProfileId = otherServerId,
                    ServerProfileName = "Ignored",
                    ServerAddress = "mqtt.other.example:1883",
                    TopicPattern = "msh/EU_433/2/e/#"
                }
            ],
            [
                new SavedChannelFilter
                {
                    Id = Guid.NewGuid(),
                    BrokerServerProfileId = activeServer.Id,
                    TopicFilter = "msh/US/2/json/#"
                },
                new SavedChannelFilter
                {
                    Id = Guid.NewGuid(),
                    BrokerServerProfileId = otherServerId,
                    TopicFilter = "msh/EU_433/2/json/#"
                }
            ]);

        Assert.True(summary.HasActiveServer);
        Assert.Equal(activeServer.Name, summary.ServerName);
        Assert.False(summary.UsesFallbackTopic);
        Assert.Equal(["msh/US/2/e/#", "msh/US/2/json/#"], summary.Topics.Select(item => item.TopicPattern));
        Assert.Equal(["Preset", "Channel"], summary.Topics.Select(item => item.SourceLabel));
    }

    [Fact]
    public void Build_ShouldUseFallbackTopic_WhenNoExplicitFiltersExist()
    {
        var activeServer = CreateServerProfile(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            "Fallback only",
            "mqtt.meshtastic.org:1883",
            "msh/US/2/e/#");

        var summary = _builder.Build(activeServer, [], []);

        Assert.True(summary.HasActiveServer);
        Assert.True(summary.UsesFallbackTopic);
        var topic = Assert.Single(summary.Topics);
        Assert.Equal("msh/US/2/e/#", topic.TopicPattern);
        Assert.Equal("Fallback", topic.SourceLabel);
        Assert.True(topic.IsFallback);
    }

    [Fact]
    public void Build_ShouldMergeDuplicateTopicSources()
    {
        var activeServer = CreateServerProfile(
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            "Merged",
            "mqtt.meshtastic.org:1883",
            "msh/US/2/e/#");

        var summary = _builder.Build(
            activeServer,
            [
                new SavedTopicPreset
                {
                    Id = Guid.NewGuid(),
                    ServerProfileId = activeServer.Id,
                    ServerProfileName = activeServer.Name,
                    ServerAddress = activeServer.ServerAddress,
                    TopicPattern = "msh/US/2/e/#"
                }
            ],
            [
                new SavedChannelFilter
                {
                    Id = Guid.NewGuid(),
                    BrokerServerProfileId = activeServer.Id,
                    TopicFilter = "msh/US/2/e/#"
                }
            ]);

        var mergedTopic = Assert.Single(summary.Topics);
        Assert.Equal("Preset + Channel", mergedTopic.SourceLabel);
    }

    private static SavedBrokerServerProfile CreateServerProfile(
        Guid id,
        string name,
        string serverAddress,
        string defaultTopicPattern)
    {
        return new SavedBrokerServerProfile
        {
            Id = id,
            Name = name,
            Host = serverAddress.Split(':')[0],
            Port = int.Parse(serverAddress.Split(':')[1]),
            ServerAddress = serverAddress,
            DefaultTopicPattern = defaultTopicPattern
        };
    }
}
