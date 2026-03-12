using MeshBoard.Contracts.Topics;

namespace MeshBoard.Contracts.Configuration;

public static class BrokerPreferenceRequestMappingExtensions
{
    public static SaveBrokerServerProfileRequest ToSaveBrokerServerProfileRequest(
        this SaveBrokerPreferenceRequest request,
        BrokerServerProfile? existingProfile = null)
    {
        return new SaveBrokerServerProfileRequest
        {
            Id = existingProfile?.Id,
            Name = request.Name,
            Host = request.Host,
            Port = request.Port,
            UseTls = request.UseTls,
            Username = request.Username,
            Password = ResolvePassword(request, existingProfile),
            DefaultTopicPattern = request.DefaultTopicPattern,
            DefaultEncryptionKeyBase64 = existingProfile?.DefaultEncryptionKeyBase64 ?? TopicEncryptionKey.DefaultKeyBase64,
            DownlinkTopic = request.DownlinkTopic,
            EnableSend = request.EnableSend,
            IsActive = existingProfile?.IsActive ?? false
        };
    }

    private static string ResolvePassword(SaveBrokerPreferenceRequest request, BrokerServerProfile? existingProfile)
    {
        if (request.ClearPassword)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            return request.Password;
        }

        return existingProfile?.Password ?? string.Empty;
    }
}
