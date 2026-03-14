using System.Text;
using System.Text.Json;
using MeshBoard.Client.Realtime;
using MeshBoard.Contracts.Realtime;

namespace MeshBoard.UnitTests;

public sealed class RealtimePacketEnvelopeParserTests
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void TryParse_WhenPayloadContainsValidEnvelope_ShouldReturnParsedEnvelope()
    {
        var parser = new RealtimePacketEnvelopeParser();
        var envelope = new RealtimePacketEnvelope
        {
            WorkspaceId = "workspace-a",
            BrokerServer = "broker.meshboard.test",
            Topic = "msh/US/2/e/LongFast/!abc",
            Payload = [1, 2, 3],
            ReceivedAtUtc = DateTimeOffset.Parse("2026-03-14T12:00:00Z")
        };
        var payloadBase64 = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope, JsonSerializerOptions)));

        var parsed = parser.TryParse(payloadBase64, out var parsedEnvelope);

        Assert.True(parsed);
        Assert.NotNull(parsedEnvelope);
        Assert.Equal(envelope.WorkspaceId, parsedEnvelope!.WorkspaceId);
        Assert.Equal(envelope.BrokerServer, parsedEnvelope.BrokerServer);
        Assert.Equal(envelope.Topic, parsedEnvelope.Topic);
        Assert.Equal(envelope.Payload, parsedEnvelope.Payload);
        Assert.Equal(envelope.ReceivedAtUtc, parsedEnvelope.ReceivedAtUtc);
    }

    [Fact]
    public void TryParse_WhenPayloadBase64IsInvalid_ShouldReturnFalse()
    {
        var parser = new RealtimePacketEnvelopeParser();

        var parsed = parser.TryParse("not-base64", out var parsedEnvelope);

        Assert.False(parsed);
        Assert.Null(parsedEnvelope);
    }

    [Fact]
    public void TryParse_WhenEnvelopeJsonIsInvalid_ShouldReturnFalse()
    {
        var parser = new RealtimePacketEnvelopeParser();
        var payloadBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("{ invalid json }"));

        var parsed = parser.TryParse(payloadBase64, out var parsedEnvelope);

        Assert.False(parsed);
        Assert.Null(parsedEnvelope);
    }
}
