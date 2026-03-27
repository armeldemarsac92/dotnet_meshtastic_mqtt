using System.Text.Json.Serialization;
using MeshBoard.Application.Services;
using MeshBoard.Contracts.Api;

namespace MeshBoard.Api.Realtime;

internal static class VernemqWebhookEndpoints
{
    private const string Tags = "VernemqWebhooks";
    private const string ContentType = "application/json";

    public static IEndpointRouteBuilder MapVernemqWebhookEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(ApiRoutes.VernemqWebhook.AuthOnRegisterM5, AuthorizeRegister)
            .WithName("VernemqAuthOnRegister")
            .Accepts<VernemqAuthOnRegisterM5Request>(ContentType)
            .Produces(200)
            .AllowAnonymous()
            .WithTags(Tags);

        endpoints.MapPost(ApiRoutes.VernemqWebhook.AuthOnSubscribeM5, AuthorizeSubscribe)
            .WithName("VernemqAuthOnSubscribe")
            .Accepts<VernemqAuthOnSubscribeM5Request>(ContentType)
            .Produces(200)
            .AllowAnonymous()
            .WithTags(Tags);

        endpoints.MapPost(ApiRoutes.VernemqWebhook.AuthOnPublishM5, AuthorizePublish)
            .WithName("VernemqAuthOnPublish")
            .Accepts<VernemqAuthOnPublishM5Request>(ContentType)
            .Produces(200)
            .AllowAnonymous()
            .WithTags(Tags);

        return endpoints;
    }

    private static IResult AuthorizeRegister(
        VernemqAuthOnRegisterM5Request request,
        IVernemqWebhookAuthorizationService authorizationService)
    {
        return authorizationService.IsRegisterAuthorized(request.ClientId, request.Username, request.Password)
            ? Results.Json(new { result = "ok" })
            : Results.Json(new { result = new { error = "not_allowed" } });
    }

    private static IResult AuthorizeSubscribe(
        VernemqAuthOnSubscribeM5Request request,
        IVernemqWebhookAuthorizationService authorizationService)
    {
        var decisions = authorizationService.AuthorizeSubscriptions(
            request.ClientId,
            request.Username,
            request.Topics.ToRequestedTopics());

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
    }

    private static IResult AuthorizePublish(
        VernemqAuthOnPublishM5Request request,
        IVernemqWebhookAuthorizationService authorizationService)
    {
        return authorizationService.IsPublishAuthorized(request.ClientId, request.Username, request.Topic)
            ? Results.Json(new { result = "ok" })
            : Results.Json(new { result = new { error = "not_allowed" } });
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
