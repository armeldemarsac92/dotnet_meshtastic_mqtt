using MeshBoard.Application.Services;

namespace MeshBoard.Api.Realtime;

internal static class VernemqWebhookMappingExtensions
{
    public static IReadOnlyCollection<VernemqRequestedTopic> ToRequestedTopics(
        this IEnumerable<VernemqWebhookEndpoints.VernemqSubscriptionTopic> topics)
    {
        return topics
            .Select(topic => new VernemqRequestedTopic(topic.Topic, topic.Qos))
            .ToArray();
    }
}
