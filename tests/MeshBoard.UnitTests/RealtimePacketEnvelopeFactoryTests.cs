using MeshBoard.Application.Realtime;
using MeshBoard.Contracts.Meshtastic;

namespace MeshBoard.UnitTests;

public sealed class RealtimePacketEnvelopeFactoryTests
{
    [Fact]
    public void Create_ShouldNormalizeTextFields_AndClonePayload()
    {
        var factory = new RealtimePacketEnvelopeFactory();
        var inboundMessage = new MqttInboundMessage
        {
            WorkspaceId = " workspace-1 ",
            BrokerServer = " broker.meshboard.test ",
            Topic = " msh/region/channel ",
            Payload = [1, 2, 3],
            ReceivedAtUtc = DateTimeOffset.Parse("2026-03-13T10:15:00Z")
        };

        var envelope = factory.Create(inboundMessage);

        Assert.Equal("workspace-1", envelope.WorkspaceId);
        Assert.Equal("broker.meshboard.test", envelope.BrokerServer);
        Assert.Equal("msh/region/channel", envelope.Topic);
        Assert.Equal(inboundMessage.ReceivedAtUtc, envelope.ReceivedAtUtc);
        Assert.Equal([1, 2, 3], envelope.Payload);
        Assert.NotSame(inboundMessage.Payload, envelope.Payload);
    }

    [Fact]
    public void Create_WhenPayloadChangesAfterCreation_ShouldKeepEnvelopePayloadStable()
    {
        var factory = new RealtimePacketEnvelopeFactory();
        var inboundMessage = new MqttInboundMessage
        {
            WorkspaceId = "workspace-2",
            BrokerServer = "broker.meshboard.test",
            Topic = "msh/2/longfast",
            Payload = [5, 6, 7]
        };

        var envelope = factory.Create(inboundMessage);
        inboundMessage.Payload[0] = 99;

        Assert.Equal([5, 6, 7], envelope.Payload);
    }
}
