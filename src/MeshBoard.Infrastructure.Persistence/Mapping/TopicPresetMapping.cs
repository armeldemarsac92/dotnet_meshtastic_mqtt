using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MeshBoard.Contracts.Topics;
using MeshBoard.Infrastructure.Persistence.SQL.Requests;
using MeshBoard.Infrastructure.Persistence.SQL.Responses;

namespace MeshBoard.Infrastructure.Persistence.Mapping;

internal static class TopicPresetMapping
{
    public static UpsertTopicPresetSqlRequest ToSqlRequest(this SaveTopicPresetRequest request, string brokerServer)
    {
        return new UpsertTopicPresetSqlRequest
        {
            Id = Guid.NewGuid().ToString(),
            BrokerServer = brokerServer,
            Name = request.Name,
            TopicPattern = request.TopicPattern,
            EncryptionKeyBase64 = request.EncryptionKeyBase64,
            IsDefault = request.IsDefault ? 1 : 0,
            CreatedAtUtc = DateTimeOffset.UtcNow.ToString("O")
        };
    }

    public static TopicPreset MapToTopicPreset(this TopicPresetSqlResponse response)
    {
        return new TopicPreset
        {
            Id = ParseOrDeriveGuid(response.Id),
            Name = response.Name,
            TopicPattern = response.TopicPattern,
            EncryptionKeyBase64 = response.EncryptionKeyBase64,
            IsDefault = response.IsDefault == 1,
            CreatedAtUtc = ParseOrDefault(response.CreatedAtUtc)
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
