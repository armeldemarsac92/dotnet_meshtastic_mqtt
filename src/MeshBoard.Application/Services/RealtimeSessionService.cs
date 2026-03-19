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
    Task<RealtimeSessionResponse> CreateSessionAsync(CancellationToken cancellationToken = default);
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
        _timeProvider = timeProvider;
    }

    public Task<RealtimeSessionResponse> CreateSessionAsync(CancellationToken cancellationToken = default)
    {
        var brokerUri = ValidateAndGetBrokerUri(_options);
        var userId = _currentUserContextAccessor.GetUserId();
        var workspaceId = _workspaceContextAccessor.GetWorkspaceId();
        var accessPolicy = _realtimeTopicAccessPolicyService.CreateForWorkspace(workspaceId);
        var issuedAtUtc = _timeProvider.GetUtcNow();
        var expiresAtUtc = issuedAtUtc.AddMinutes(_options.TokenLifetimeMinutes);
        var clientId = CreateClientId(_options.ClientIdPrefix);

        return Task.FromResult(new RealtimeSessionResponse
        {
            BrokerUrl = brokerUri.ToString(),
            ClientId = clientId,
            Token = CreateToken(userId, workspaceId, clientId, issuedAtUtc, expiresAtUtc, accessPolicy),
            ExpiresAtUtc = expiresAtUtc,
            AllowedTopicPatterns = accessPolicy.SubscribeTopicPatterns
        });
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

    private static Uri ValidateAndGetBrokerUri(RealtimeSessionOptions options)
    {
        if (!Uri.TryCreate(options.BrokerUrl, UriKind.Absolute, out var brokerUri))
        {
            throw new InvalidOperationException("RealtimeSession:BrokerUrl must be an absolute URI.");
        }

        if (!string.Equals(brokerUri.Scheme, "wss", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("RealtimeSession:BrokerUrl must use the wss scheme.");
        }

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

        if (string.IsNullOrWhiteSpace(options.SigningPrivateKeyPem))
        {
            throw new InvalidOperationException("RealtimeSession:SigningPrivateKeyPem is required.");
        }

        return brokerUri;
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
        rsa.ImportFromPem(_options.SigningPrivateKeyPem.AsSpan());

        var signature = rsa.SignData(
            Encoding.UTF8.GetBytes(signingInput),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        return $"{signingInput}.{Base64UrlEncode(signature)}";
    }
}
