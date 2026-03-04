using System.Globalization;
using MeshBoard.Contracts.Messages;
using MeshBoard.Infrastructure.Persistence.SQL.Responses;

namespace MeshBoard.Infrastructure.Persistence.Mapping;

internal static class MessageMapping
{
    public static IReadOnlyCollection<MessageSummary> MapToMessages(this IReadOnlyCollection<MessageSummarySqlResponse> responses)
    {
        return responses.Select(MapToMessage).ToList();
    }

    private static MessageSummary MapToMessage(MessageSummarySqlResponse response)
    {
        return new MessageSummary
        {
            Id = Guid.Parse(response.Id),
            Topic = response.Topic,
            FromNodeId = response.FromNodeId,
            ToNodeId = response.ToNodeId,
            PayloadPreview = response.PayloadPreview,
            IsPrivate = response.IsPrivate == 1,
            ReceivedAtUtc = DateTimeOffset.Parse(response.ReceivedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
        };
    }
}
