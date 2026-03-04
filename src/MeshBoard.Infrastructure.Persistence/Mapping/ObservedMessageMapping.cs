using MeshBoard.Contracts.Messages;
using MeshBoard.Infrastructure.Persistence.SQL.Requests;

namespace MeshBoard.Infrastructure.Persistence.Mapping;

internal static class ObservedMessageMapping
{
    public static SaveObservedMessageSqlRequest ToSqlRequest(this SaveObservedMessageRequest request)
    {
        return new SaveObservedMessageSqlRequest
        {
            Id = Guid.NewGuid().ToString(),
            Topic = request.Topic,
            FromNodeId = request.FromNodeId,
            ToNodeId = request.ToNodeId,
            PayloadPreview = request.PayloadPreview,
            IsPrivate = request.IsPrivate ? 1 : 0,
            ReceivedAtUtc = request.ReceivedAtUtc.ToString("O")
        };
    }
}
