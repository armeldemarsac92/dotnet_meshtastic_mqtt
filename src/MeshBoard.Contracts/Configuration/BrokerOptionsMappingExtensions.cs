using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace MeshBoard.Contracts.Configuration;

public static class BrokerOptionsMappingExtensions
{
    public static SaveBrokerServerProfileRequest ToDefaultSaveBrokerServerProfileRequest(this BrokerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new SaveBrokerServerProfileRequest
        {
            Name = "Default server",
            Host = options.Host,
            Port = options.Port,
            UseTls = options.UseTls,
            Username = options.Username,
            Password = options.Password,
            DownlinkTopic = options.DownlinkTopic,
            EnableSend = options.EnableSend,
            IsActive = true
        };
    }

    public static BrokerServerProfile ToCollectorBrokerServerProfile(this BrokerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new BrokerServerProfile
        {
            Id = CreateDeterministicId(options),
            Name = $"Collector upstream ({options.Host}:{options.Port})",
            Host = options.Host,
            Port = options.Port,
            UseTls = options.UseTls,
            Username = options.Username,
            Password = options.Password,
            DownlinkTopic = options.DownlinkTopic,
            EnableSend = options.EnableSend,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UnixEpoch
        };
    }

    private static Guid CreateDeterministicId(BrokerOptions options)
    {
        var material = string.Join(
            '|',
            options.Host.Trim(),
            options.Port.ToString(CultureInfo.InvariantCulture),
            options.UseTls ? "tls" : "plain",
            options.Username.Trim(),
            options.DownlinkTopic.Trim(),
            options.EnableSend ? "send" : "recv");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        var bytes = new byte[16];
        Array.Copy(hash, bytes, bytes.Length);
        return new Guid(bytes);
    }
}
