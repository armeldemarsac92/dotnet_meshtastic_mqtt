using MeshBoard.Client.Messages;
using MeshBoard.Client.Realtime;

namespace MeshBoard.UnitTests;

public sealed class LiveMessageFeedServiceTests
{
    [Fact]
    public void RecordMessage_ShouldUseParsedEnvelopeFields()
    {
        var service = new LiveMessageFeedService(new LiveMessageFeedState());
        var rawPacket = new RealtimeRawPacketEvent
        {
            WorkspaceId = "workspace-a",
            BrokerServer = "broker.meshboard.test",
            SourceTopic = "msh/US/2/e/LongFast/!abc",
            DownstreamTopic = "meshboard/workspaces/workspace-a/live/packets",
            PayloadBase64 = Convert.ToBase64String([10, 20, 30]),
            PayloadSizeBytes = 3,
            ReceivedAtUtc = DateTimeOffset.Parse("2026-03-14T14:00:00Z")
        };

        service.RecordMessage(rawPacket);

        var message = Assert.Single(service.Current.Messages);
        Assert.Equal("workspace-a", message.WorkspaceId);
        Assert.Equal("broker.meshboard.test", message.BrokerServer);
        Assert.Equal("meshboard/workspaces/workspace-a/live/packets", message.DownstreamTopic);
        Assert.Equal("msh/US/2/e/LongFast/!abc", message.SourceTopic);
        Assert.Equal(3, message.PayloadSizeBytes);
        Assert.Equal(Convert.ToBase64String([10, 20, 30]), message.PayloadBase64);
        Assert.Equal(rawPacket.ReceivedAtUtc, message.ReceivedAtUtc);
    }

    [Fact]
    public void RecordMessage_ShouldRetainOnlyMostRecentHundredMessages()
    {
        var service = new LiveMessageFeedService(new LiveMessageFeedState());

        for (var index = 0; index < 110; index++)
        {
            service.RecordMessage(
                new RealtimeRawPacketEvent
                {
                    WorkspaceId = "workspace-a",
                    BrokerServer = "broker.meshboard.test",
                    SourceTopic = $"msh/US/2/e/LongFast/{index}",
                    DownstreamTopic = "meshboard/workspaces/workspace-a/live/packets",
                    PayloadBase64 = Convert.ToBase64String([(byte)index]),
                    PayloadSizeBytes = 1,
                    ReceivedAtUtc = DateTimeOffset.UnixEpoch.AddSeconds(index)
                });
        }

        Assert.Equal(100, service.Current.Messages.Count);
        Assert.Equal(110, service.Current.TotalReceived);
        Assert.Equal("msh/US/2/e/LongFast/109", service.Current.Messages[0].SourceTopic);
        Assert.Equal("msh/US/2/e/LongFast/10", service.Current.Messages[^1].SourceTopic);
    }
}
