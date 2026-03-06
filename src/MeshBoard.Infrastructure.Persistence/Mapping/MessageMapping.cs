using System.Globalization;
using System.Security.Cryptography;
using System.Text;
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
            Id = ParseOrDeriveGuid(response.Id),
            BrokerServer = response.BrokerServer,
            Topic = response.Topic,
            PacketType = response.PacketType,
            FromNodeId = response.FromNodeId,
            FromNodeShortName = response.FromNodeShortName,
            FromNodeLongName = response.FromNodeLongName,
            ToNodeId = response.ToNodeId,
            PayloadPreview = response.PayloadPreview,
            IsPrivate = response.IsPrivate == 1,
            ReceivedAtUtc = ParseOrDefault(response.ReceivedAtUtc)
        };
    }

    private static Guid ParseOrDeriveGuid(string? value)
    {
        if (Guid.TryParse(value, out var parsedGuid))
        {
            return parsedGuid;
        }

        var hash = MD5.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty));
        return new Guid(hash);
    }

    private static DateTimeOffset ParseOrDefault(string value)
    {
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsedValue)
            ? parsedValue
            : DateTimeOffset.UnixEpoch;
    }
}
