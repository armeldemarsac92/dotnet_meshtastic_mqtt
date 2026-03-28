using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MeshBoard.Application.Abstractions.Authentication;
using MeshBoard.Application.Abstractions.Workspaces;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Realtime;
using Microsoft.Extensions.Options;

namespace MeshBoard.Application.Services;

public interface IRealtimeSessionService
{
    Task<RealtimeSessionResponse> CreateSessionAsync(
        Uri? requestOrigin = null,
        CancellationToken cancellationToken = default);
}

public sealed class RealtimeSessionService : IRealtimeSessionService
{
    private const string JwtAlgorithm = "RS256";
    private const int MaxClientIdLength = 23;
    private const int MaxClientIdPrefixLength = 12;

    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly ICurrentUserContextAccessor _currentUserContextAccessor;
    private readonly IRealtimeTopicAccessPolicyService _realtimeTopicAccessPolicyService;
    private readonly RealtimeSessionOptions _options;
    private readonly string _signingPrivateKeyPem;
    private readonly TimeProvider _timeProvider;
    private readonly IWorkspaceContextAccessor _workspaceContextAccessor;

    public RealtimeSessionService(
        ICurrentUserContextAccessor currentUserContextAccessor,
        IWorkspaceContextAccessor workspaceContextAccessor,
        IRealtimeTopicAccessPolicyService realtimeTopicAccessPolicyService,
        IOptions<RealtimeSessionOptions> options,
        TimeProvider timeProvider)
    {
        _currentUserContextAccessor = currentUserContextAccessor;
        _workspaceContextAccessor = workspaceContextAccessor;
        _realtimeTopicAccessPolicyService = realtimeTopicAccessPolicyService;
        _options = options.Value;
        _signingPrivateKeyPem = RealtimeSigningKeyMaterialResolver.ResolvePrivateKeyPem(_options);
        _timeProvider = timeProvider;
    }

    public Task<RealtimeSessionResponse> CreateSessionAsync(
        Uri? requestOrigin = null,
        CancellationToken cancellationToken = default)
    {
        ValidateOptions(_options);

        var brokerUri = ResolveBrokerUri(_options, requestOrigin);
        var userId = _currentUserContextAccessor.GetUserId();
        var workspaceId = _workspaceContextAccessor.GetWorkspaceId();
        var accessPolicy = _realtimeTopicAccessPolicyService.CreateForWorkspace(workspaceId);
        var issuedAtUtc = _timeProvider.GetUtcNow();
        var expiresAtUtc = issuedAtUtc.AddMinutes(_options.TokenLifetimeMinutes);
        var clientId = CreateClientId(_options.ClientIdPrefix);

        return Task.FromResult(
            accessPolicy.ToRealtimeSessionResponse(
                brokerUri,
                clientId,
                CreateToken(userId, workspaceId, clientId, issuedAtUtc, expiresAtUtc, accessPolicy),
                expiresAtUtc));
    }

    private static string Base64UrlEncode(byte[] value)
    {
        return Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string CreateClientId(string prefix)
    {
        var normalizedPrefix = NormalizeClientIdPrefix(prefix);
        var suffixLength = Math.Max(8, MaxClientIdLength - normalizedPrefix.Length - 1);
        var suffix = Guid.NewGuid().ToString("N")[..Math.Min(suffixLength, 32)];

        return $"{normalizedPrefix}-{suffix}";
    }

    private static string NormalizeClientIdPrefix(string prefix)
    {
        var sanitized = string.Concat(
                prefix
                    .Trim()
                    .ToLowerInvariant()
                    .Where(character => char.IsAsciiLetterOrDigit(character) || character == '-'))
            .Trim('-');

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return "mb";
        }

        return sanitized.Length <= MaxClientIdPrefixLength
            ? sanitized
            : sanitized[..MaxClientIdPrefixLength];
    }

    private static void ValidateOptions(RealtimeSessionOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            throw new InvalidOperationException("RealtimeSession:Issuer is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            throw new InvalidOperationException("RealtimeSession:Audience is required.");
        }

        if (string.IsNullOrWhiteSpace(options.KeyId))
        {
            throw new InvalidOperationException("RealtimeSession:KeyId is required.");
        }

        if (options.TokenLifetimeMinutes <= 0)
        {
            throw new InvalidOperationException("RealtimeSession:TokenLifetimeMinutes must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(options.SigningPrivateKeyPem) &&
            string.IsNullOrWhiteSpace(options.SigningPrivateKeyPemFile))
        {
            throw new InvalidOperationException(
                "RealtimeSession:SigningPrivateKeyPem or RealtimeSession:SigningPrivateKeyPemFile is required.");
        }
    }

    private static Uri ResolveBrokerUri(RealtimeSessionOptions options, Uri? requestOrigin)
    {
        return options.UseRequestOriginBrokerUrl
            ? ResolveBrokerUriFromRequestOrigin(options, requestOrigin)
            : ValidateConfiguredBrokerUri(options);
    }

    private static Uri ResolveBrokerUriFromRequestOrigin(RealtimeSessionOptions options, Uri? requestOrigin)
    {
        if (requestOrigin is null || !requestOrigin.IsAbsoluteUri)
        {
            throw new InvalidOperationException(
                "RealtimeSession:UseRequestOriginBrokerUrl requires an absolute request origin.");
        }

        var scheme = requestOrigin.Scheme.ToLowerInvariant() switch
        {
            "https" => "wss",
            "http" when options.AllowInsecureBrokerUrl => "ws",
            "http" => throw new InvalidOperationException(
                "RealtimeSession:UseRequestOriginBrokerUrl requires https unless AllowInsecureBrokerUrl is enabled."),
            _ => throw new InvalidOperationException(
                "RealtimeSession:UseRequestOriginBrokerUrl only supports http or https request origins.")
        };

        var brokerPath = string.IsNullOrWhiteSpace(options.BrokerPath)
            ? "/mqtt"
            : options.BrokerPath.Trim();

        if (!brokerPath.StartsWith('/'))
        {
            brokerPath = $"/{brokerPath}";
        }

        var builder = new UriBuilder(requestOrigin)
        {
            Scheme = scheme,
            Port = requestOrigin.IsDefaultPort ? -1 : requestOrigin.Port,
            Path = brokerPath,
            Query = string.Empty,
            Fragment = string.Empty
        };

        return builder.Uri;
    }

    private static Uri ValidateConfiguredBrokerUri(RealtimeSessionOptions options)
    {
        if (!Uri.TryCreate(options.BrokerUrl, UriKind.Absolute, out var brokerUri))
        {
            throw new InvalidOperationException("RealtimeSession:BrokerUrl must be an absolute URI.");
        }

        ValidateBrokerScheme(options, brokerUri);
        return brokerUri;
    }

    private static void ValidateBrokerScheme(RealtimeSessionOptions options, Uri brokerUri)
    {
        var isSecureWebSocket = string.Equals(brokerUri.Scheme, "wss", StringComparison.OrdinalIgnoreCase);
        var isInsecureWebSocket = string.Equals(brokerUri.Scheme, "ws", StringComparison.OrdinalIgnoreCase);

        if (!isSecureWebSocket && !(options.AllowInsecureBrokerUrl && isInsecureWebSocket))
        {
            throw new InvalidOperationException(
                options.AllowInsecureBrokerUrl
                    ? "RealtimeSession broker URLs must use the ws or wss scheme."
                    : "RealtimeSession broker URLs must use the wss scheme.");
        }
    }

    private string CreateToken(
        string userId,
        string workspaceId,
        string clientId,
        DateTimeOffset issuedAtUtc,
        DateTimeOffset expiresAtUtc,
        RealtimeTopicAccessPolicy accessPolicy)
    {
        var header = new Dictionary<string, object?>
        {
            ["alg"] = JwtAlgorithm,
            ["kid"] = _options.KeyId,
            ["typ"] = "JWT"
        };

        var payload = new Dictionary<string, object?>
        {
            ["iss"] = _options.Issuer,
            ["aud"] = _options.Audience,
            ["sub"] = userId,
            ["jti"] = Guid.NewGuid().ToString("N"),
            ["iat"] = issuedAtUtc.ToUnixTimeSeconds(),
            ["nbf"] = issuedAtUtc.ToUnixTimeSeconds(),
            ["exp"] = expiresAtUtc.ToUnixTimeSeconds(),
            ["workspace_id"] = workspaceId,
            ["user_id"] = userId,
            ["client_id"] = clientId,
            ["allowed_topic_patterns"] = accessPolicy.SubscribeTopicPatterns,
            ["acl"] = new Dictionary<string, object?>
            {
                ["subscribe"] = accessPolicy.SubscribeTopicPatterns,
                ["publish"] = accessPolicy.PublishTopicPatterns
            }
        };

        var encodedHeader = Base64UrlEncode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(header, JsonSerializerOptions)));
        var encodedPayload = Base64UrlEncode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonSerializerOptions)));
        var signingInput = $"{encodedHeader}.{encodedPayload}";

        using var rsa = RSA.Create();
        rsa.ImportFromPem(_signingPrivateKeyPem.AsSpan());

        var signature = rsa.SignData(
            Encoding.UTF8.GetBytes(signingInput),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        return $"{signingInput}.{Base64UrlEncode(signature)}";
    }
}
