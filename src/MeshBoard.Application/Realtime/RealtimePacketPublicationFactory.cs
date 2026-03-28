using MeshBoard.Application.Abstractions.Realtime;
using MeshBoard.Contracts.Realtime;
using System.Text;
using System.Text.Json;

namespace MeshBoard.Application.Realtime;

public sealed class RealtimePacketPublicationFactory : IRealtimePacketPublicationFactory
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public RealtimePacketPublication Create(RealtimePacketEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        return new RealtimePacketPublication
        {
            Topic = RealtimeTopicNames.BuildWorkspacePacketTopic(envelope.WorkspaceId),
            ContentType = "application/json",
            Payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope, JsonSerializerOptions))
        };
    }
}
