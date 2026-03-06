using System.Globalization;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Infrastructure.Persistence.SQL.Requests;
using MeshBoard.Infrastructure.Persistence.SQL.Responses;

namespace MeshBoard.Infrastructure.Persistence.Mapping;

internal static class BrokerServerProfileMapping
{
    public static UpsertBrokerServerProfileSqlRequest ToSqlRequest(this SaveBrokerServerProfileRequest request)
    {
        return new UpsertBrokerServerProfileSqlRequest
        {
            Id = (request.Id ?? Guid.NewGuid()).ToString(),
            Name = request.Name,
            Host = request.Host,
            Port = request.Port,
            UseTls = request.UseTls ? 1 : 0,
            Username = request.Username,
            Password = request.Password,
            DefaultTopicPattern = request.DefaultTopicPattern,
            DefaultEncryptionKeyBase64 = request.DefaultEncryptionKeyBase64,
            DownlinkTopic = request.DownlinkTopic,
            EnableSend = request.EnableSend ? 1 : 0,
            IsActive = request.IsActive ? 1 : 0,
            CreatedAtUtc = DateTimeOffset.UtcNow.ToString("O")
        };
    }

    public static BrokerServerProfile MapToBrokerServerProfile(this BrokerServerProfileSqlResponse response)
    {
        return new BrokerServerProfile
        {
            Id = Guid.Parse(response.Id),
            Name = response.Name,
            Host = response.Host,
            Port = response.Port,
            UseTls = response.UseTls == 1,
            Username = response.Username,
            Password = response.Password,
            DefaultTopicPattern = response.DefaultTopicPattern,
            DefaultEncryptionKeyBase64 = response.DefaultEncryptionKeyBase64,
            DownlinkTopic = response.DownlinkTopic,
            EnableSend = response.EnableSend == 1,
            IsActive = response.IsActive == 1,
            CreatedAtUtc = ParseOrDefault(response.CreatedAtUtc)
        };
    }

    private static DateTimeOffset ParseOrDefault(string value)
    {
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsedValue)
            ? parsedValue
            : DateTimeOffset.UtcNow;
    }
}
