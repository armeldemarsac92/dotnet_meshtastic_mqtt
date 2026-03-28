using MeshBoard.Application.Realtime;
using MeshBoard.Contracts.Realtime;
using System.Text;
using System.Text.Json;

namespace MeshBoard.UnitTests;

public sealed class RealtimePacketPublicationFactoryTests
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Create_ShouldRouteToWorkspacePacketTopic_AndClonePayload()
    {
        var factory = new RealtimePacketPublicationFactory();
        var envelope = new RealtimePacketEnvelope
        {
            WorkspaceId = "workspace-a",
            BrokerServer = "broker.meshboard.test",
            Topic = "msh/region/channel",
            Payload = [8, 9, 10],
            ReceivedAtUtc = DateTimeOffset.Parse("2026-03-14T09:30:00Z")
        };

        var publication = factory.Create(envelope);
        var roundTrippedEnvelope = JsonSerializer.Deserialize<RealtimePacketEnvelope>(
            Encoding.UTF8.GetString(publication.Payload),
            JsonSerializerOptions);

        Assert.Equal("meshboard/workspaces/workspace-a/live/packets", publication.Topic);
        Assert.Equal("application/json", publication.ContentType);
        Assert.NotNull(roundTrippedEnvelope);
        Assert.Equal(envelope.WorkspaceId, roundTrippedEnvelope!.WorkspaceId);
        Assert.Equal(envelope.BrokerServer, roundTrippedEnvelope.BrokerServer);
        Assert.Equal(envelope.Topic, roundTrippedEnvelope.Topic);
        Assert.Equal(envelope.ReceivedAtUtc, roundTrippedEnvelope.ReceivedAtUtc);
        Assert.Equal([8, 9, 10], roundTrippedEnvelope.Payload);
    }

    [Fact]
    public void Create_WhenWorkspaceIdIsInvalid_ShouldThrow()
    {
        var factory = new RealtimePacketPublicationFactory();
        var envelope = new RealtimePacketEnvelope
        {
            WorkspaceId = "workspace/a",
            Payload = [1]
        };

        Assert.Throws<ArgumentException>(() => factory.Create(envelope));
    }
}
