namespace MeshBoard.Contracts.Configuration;

public static class BrokerServerProfileMappingExtensions
{
    public static SavedBrokerServerProfile ToSavedBrokerServerProfile(this BrokerServerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return new SavedBrokerServerProfile
        {
            Id = profile.Id,
            Name = profile.Name,
            Host = profile.Host,
            Port = profile.Port,
            UseTls = profile.UseTls,
            Username = profile.Username,
            HasPasswordConfigured = !string.IsNullOrWhiteSpace(profile.Password),
            DownlinkTopic = profile.DownlinkTopic,
            EnableSend = profile.EnableSend,
            IsActive = profile.IsActive,
            CreatedAtUtc = profile.CreatedAtUtc,
            ServerAddress = profile.ServerAddress
        };
    }
}
