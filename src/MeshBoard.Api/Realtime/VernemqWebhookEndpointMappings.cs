using System.Text.Json.Serialization;
using MeshBoard.Application.Services;
using MeshBoard.Contracts.Api;

namespace MeshBoard.Api.Realtime;

internal static class VernemqWebhookEndpointMappings
{
    public static IEndpointRouteBuilder MapVernemqWebhookEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(ApiRoutes.VernemqWebhook.Group)
            .AllowAnonymous();

        group.MapPost(
            ApiRoutes.VernemqWebhook.AuthOnRegisterM5,
            (VernemqAuthOnRegisterM5Request request, IVernemqWebhookAuthorizationService authorizationService) =>
            {
                return authorizationService.IsRegisterAuthorized(request.ClientId, request.Username, request.Password)
                    ? Results.Json(new { result = "ok" })
                    : Results.Json(new { result = new { error = "not_allowed" } });
            });

        group.MapPost(
            ApiRoutes.VernemqWebhook.AuthOnSubscribeM5,
            (VernemqAuthOnSubscribeM5Request request, IVernemqWebhookAuthorizationService authorizationService) =>
            {
                var decisions = authorizationService.AuthorizeSubscriptions(
                    request.ClientId,
                    request.Username,
                    request.Topics
                        .Select(topic => new VernemqRequestedTopic(topic.Topic, topic.Qos))
                        .ToArray());

                return Results.Json(
                    new
                    {
                        result = "ok",
                        modifiers = new
                        {
                            topics = decisions.Select(
                                decision => new
                                {
                                    topic = decision.Topic,
                                    qos = decision.Qos
                                })
                        }
                    });
            });

        group.MapPost(
            ApiRoutes.VernemqWebhook.AuthOnPublishM5,
            (VernemqAuthOnPublishM5Request request, IVernemqWebhookAuthorizationService authorizationService) =>
            {
                return authorizationService.IsPublishAuthorized(request.ClientId, request.Username, request.Topic)
                    ? Results.Json(new { result = "ok" })
                    : Results.Json(new { result = new { error = "not_allowed" } });
            });

        return endpoints;
    }

    public sealed class VernemqAuthOnRegisterM5Request
    {
        [JsonPropertyName("client_id")]
        public string ClientId { get; set; } = string.Empty;

        [JsonPropertyName("clean_start")]
        public bool CleanStart { get; set; }

        [JsonPropertyName("password")]
        public string? Password { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }
    }

    public sealed class VernemqAuthOnPublishM5Request
    {
        [JsonPropertyName("client_id")]
        public string ClientId { get; set; } = string.Empty;

        [JsonPropertyName("topic")]
        public string Topic { get; set; } = string.Empty;

        [JsonPropertyName("username")]
        public string? Username { get; set; }
    }

    public sealed class VernemqAuthOnSubscribeM5Request
    {
        [JsonPropertyName("client_id")]
        public string ClientId { get; set; } = string.Empty;

        [JsonPropertyName("topics")]
        public List<VernemqSubscriptionTopic> Topics { get; set; } = [];

        [JsonPropertyName("username")]
        public string? Username { get; set; }
    }

    public sealed class VernemqSubscriptionTopic
    {
        [JsonPropertyName("qos")]
        public int Qos { get; set; }

        [JsonPropertyName("topic")]
        public string Topic { get; set; } = string.Empty;
    }
}
