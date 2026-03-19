using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MeshBoard.Contracts.Configuration;
using Microsoft.Extensions.Options;

namespace MeshBoard.Application.Services;

public interface IVernemqWebhookAuthorizationService
{
    IReadOnlyList<VernemqSubscriptionAuthorizationDecision> AuthorizeSubscriptions(
        string clientId,
        string? sessionToken,
        IReadOnlyCollection<VernemqRequestedTopic> topics);

    bool IsPublishAuthorized(string clientId, string? sessionToken, string topic);

    bool IsRegisterAuthorized(string clientId, string? sessionToken);
}

public sealed class VernemqWebhookAuthorizationService : IVernemqWebhookAuthorizationService
{
    private const int MqttNotAuthorizedQos = 135;
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly RealtimeSessionOptions _options;
    private readonly RSAParameters _signingPublicKey;
    private readonly TimeProvider _timeProvider;
    private readonly IRealtimeTopicFilterAuthorizationService _topicFilterAuthorizationService;

    public VernemqWebhookAuthorizationService(
        IOptions<RealtimeSessionOptions> options,
        TimeProvider timeProvider,
        IRealtimeTopicFilterAuthorizationService topicFilterAuthorizationService)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
        _topicFilterAuthorizationService = topicFilterAuthorizationService;
        var signingPrivateKeyPem = RealtimeSigningKeyMaterialResolver.ResolvePrivateKeyPem(_options);

        using var rsa = RSA.Create();
        rsa.ImportFromPem(signingPrivateKeyPem.AsSpan());
        _signingPublicKey = rsa.ExportParameters(false);
    }

    public bool IsRegisterAuthorized(string clientId, string? sessionToken)
    {
        return TryValidateSessionToken(clientId, sessionToken, out _);
    }

    public IReadOnlyList<VernemqSubscriptionAuthorizationDecision> AuthorizeSubscriptions(
        string clientId,
        string? sessionToken,
        IReadOnlyCollection<VernemqRequestedTopic> topics)
    {
        if (!TryValidateSessionToken(clientId, sessionToken, out var validatedSession))
        {
            return topics
                .Select(topic => new VernemqSubscriptionAuthorizationDecision(topic.Topic, MqttNotAuthorizedQos))
                .ToArray();
        }

        return topics
            .Select(
                topic => new VernemqSubscriptionAuthorizationDecision(
                    topic.Topic,
                    _topicFilterAuthorizationService.IsSubscriptionAllowed(
                        topic.Topic,
                        validatedSession.SubscribeTopicPatterns)
                        ? topic.Qos
                        : MqttNotAuthorizedQos))
            .ToArray();
    }

    public bool IsPublishAuthorized(string clientId, string? sessionToken, string topic)
    {
        return TryValidateSessionToken(clientId, sessionToken, out var validatedSession)
               && _topicFilterAuthorizationService.IsPublishAllowed(topic, validatedSession.PublishTopicPatterns);
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value
            .Replace('-', '+')
            .Replace('_', '/');

        var paddingLength = (4 - padded.Length % 4) % 4;
        if (paddingLength > 0)
        {
            padded = padded.PadRight(padded.Length + paddingLength, '=');
        }

        return Convert.FromBase64String(padded);
    }

    private static IReadOnlyList<string> ExtractStringArray(JsonElement payload, string propertyName)
    {
        if (!payload.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return property
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> ExtractTopicPatterns(JsonElement payload)
    {
        if (payload.TryGetProperty("acl", out var aclProperty)
            && aclProperty.ValueKind == JsonValueKind.Object)
        {
            return ExtractStringArray(aclProperty, "subscribe");
        }

        return ExtractStringArray(payload, "allowed_topic_patterns");
    }

    private static IReadOnlyList<string> ExtractPublishTopicPatterns(JsonElement payload)
    {
        if (payload.TryGetProperty("acl", out var aclProperty)
            && aclProperty.ValueKind == JsonValueKind.Object)
        {
            return ExtractStringArray(aclProperty, "publish");
        }

        return Array.Empty<string>();
    }

    private static bool IsAudienceValid(JsonElement payload, string expectedAudience)
    {
        if (!payload.TryGetProperty("aud", out var audienceProperty))
        {
            return false;
        }

        return audienceProperty.ValueKind switch
        {
            JsonValueKind.String => string.Equals(audienceProperty.GetString(), expectedAudience, StringComparison.Ordinal),
            JsonValueKind.Array => audienceProperty
                .EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString())
                .Any(audience => string.Equals(audience, expectedAudience, StringComparison.Ordinal)),
            _ => false
        };
    }

    private static bool TryReadString(JsonElement payload, string propertyName, out string value)
    {
        if (payload.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(property.GetString()))
        {
            value = property.GetString()!.Trim();
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryReadUnixTime(JsonElement payload, string propertyName, out DateTimeOffset value)
    {
        if (payload.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt64(out var unixTimeSeconds))
        {
            value = DateTimeOffset.FromUnixTimeSeconds(unixTimeSeconds);
            return true;
        }

        value = default;
        return false;
    }

    private bool TryValidateSessionToken(
        string clientId,
        string? sessionToken,
        out ValidatedRealtimeBrokerSession validatedSession)
    {
        validatedSession = default!;

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(sessionToken))
        {
            return false;
        }

        var tokenSegments = sessionToken.Split('.');
        if (tokenSegments.Length != 3)
        {
            return false;
        }

        JsonElement header;
        JsonElement payload;

        try
        {
            using var headerDocument = JsonDocument.Parse(Base64UrlDecode(tokenSegments[0]));
            using var payloadDocument = JsonDocument.Parse(Base64UrlDecode(tokenSegments[1]));

            header = headerDocument.RootElement.Clone();
            payload = payloadDocument.RootElement.Clone();
        }
        catch (Exception)
        {
            return false;
        }

        if (!TryReadString(header, "alg", out var algorithm)
            || !string.Equals(algorithm, "RS256", StringComparison.Ordinal))
        {
            return false;
        }

        if (TryReadString(header, "kid", out var keyId)
            && !string.Equals(keyId, _options.KeyId, StringComparison.Ordinal))
        {
            return false;
        }

        if (!VerifySignature(tokenSegments))
        {
            return false;
        }

        if (!TryReadString(payload, "iss", out var issuer)
            || !string.Equals(issuer, _options.Issuer, StringComparison.Ordinal)
            || !IsAudienceValid(payload, _options.Audience))
        {
            return false;
        }

        if (!TryReadUnixTime(payload, "nbf", out var notBeforeUtc)
            || !TryReadUnixTime(payload, "exp", out var expiresAtUtc))
        {
            return false;
        }

        var nowUtc = _timeProvider.GetUtcNow();
        if (notBeforeUtc > nowUtc || expiresAtUtc <= nowUtc)
        {
            return false;
        }

        if (!TryReadString(payload, "client_id", out var tokenClientId)
            || !string.Equals(tokenClientId, clientId.Trim(), StringComparison.Ordinal))
        {
            return false;
        }

        if (!TryReadString(payload, "user_id", out var userId)
            || !TryReadString(payload, "workspace_id", out var workspaceId))
        {
            return false;
        }

        validatedSession = new ValidatedRealtimeBrokerSession(
            userId,
            workspaceId,
            tokenClientId,
            ExtractTopicPatterns(payload),
            ExtractPublishTopicPatterns(payload));

        return true;
    }

    private bool VerifySignature(IReadOnlyList<string> tokenSegments)
    {
        using var rsa = RSA.Create();
        rsa.ImportParameters(_signingPublicKey);

        return rsa.VerifyData(
            Encoding.UTF8.GetBytes($"{tokenSegments[0]}.{tokenSegments[1]}"),
            Base64UrlDecode(tokenSegments[2]),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
    }

    private sealed record ValidatedRealtimeBrokerSession(
        string UserId,
        string WorkspaceId,
        string ClientId,
        IReadOnlyList<string> SubscribeTopicPatterns,
        IReadOnlyList<string> PublishTopicPatterns);
}

public sealed record VernemqRequestedTopic(string Topic, int Qos);

public sealed record VernemqSubscriptionAuthorizationDecision(string Topic, int Qos);
