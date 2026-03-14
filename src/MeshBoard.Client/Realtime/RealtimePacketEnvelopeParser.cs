using System.Text;
using System.Text.Json;
using MeshBoard.Contracts.Realtime;

namespace MeshBoard.Client.Realtime;

public sealed class RealtimePacketEnvelopeParser
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public bool TryParse(string payloadBase64, out RealtimePacketEnvelope? envelope)
    {
        envelope = null;

        if (string.IsNullOrWhiteSpace(payloadBase64))
        {
            return false;
        }

        try
        {
            var payloadBytes = Convert.FromBase64String(payloadBase64);
            var parsedEnvelope = JsonSerializer.Deserialize<RealtimePacketEnvelope>(
                Encoding.UTF8.GetString(payloadBytes),
                JsonSerializerOptions);

            if (parsedEnvelope is null
                || string.IsNullOrWhiteSpace(parsedEnvelope.WorkspaceId)
                || string.IsNullOrWhiteSpace(parsedEnvelope.BrokerServer)
                || string.IsNullOrWhiteSpace(parsedEnvelope.Topic))
            {
                return false;
            }

            envelope = new RealtimePacketEnvelope
            {
                WorkspaceId = parsedEnvelope.WorkspaceId.Trim(),
                BrokerServer = parsedEnvelope.BrokerServer.Trim(),
                Topic = parsedEnvelope.Topic.Trim(),
                Payload = parsedEnvelope.Payload.ToArray(),
                ReceivedAtUtc = parsedEnvelope.ReceivedAtUtc
            };

            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
