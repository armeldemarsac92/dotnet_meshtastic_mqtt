namespace MeshBoard.Contracts.Configuration;

public static class SaveBrokerServerProfileRequestMappingExtensions
{
    public static SaveBrokerServerProfileRequest Normalize(this SaveBrokerServerProfileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new SaveBrokerServerProfileRequest
        {
            Id = request.Id,
            Name = request.Name.Trim(),
            Host = request.Host.Trim(),
            Port = request.Port,
            UseTls = request.UseTls,
            Username = request.Username.Trim(),
            Password = request.Password,
            DownlinkTopic = request.DownlinkTopic.Trim(),
            EnableSend = request.EnableSend,
            IsActive = request.IsActive
        };
    }

    public static SaveBrokerServerProfileRequest Clone(this SaveBrokerServerProfileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new SaveBrokerServerProfileRequest
        {
            Id = request.Id,
            Name = request.Name,
            Host = request.Host,
            Port = request.Port,
            UseTls = request.UseTls,
            Username = request.Username,
            Password = request.Password,
            DownlinkTopic = request.DownlinkTopic,
            EnableSend = request.EnableSend,
            IsActive = request.IsActive
        };
    }
}
