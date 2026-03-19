using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Topics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MeshBoard.Infrastructure.Meshtastic.Decoding;

internal sealed class TopicPresetEncryptionKeyResolver : ITopicEncryptionKeyResolver
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(2);

    private readonly object _cacheSync = new();
    private readonly byte[] _fallbackDefaultKeyBytes;
    private readonly ILogger<TopicPresetEncryptionKeyResolver> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly Dictionary<string, WorkspaceKeyCache> _cacheByWorkspace = new(StringComparer.Ordinal);

    public TopicPresetEncryptionKeyResolver(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<BrokerOptions> brokerOptions,
        ILogger<TopicPresetEncryptionKeyResolver> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _fallbackDefaultKeyBytes = ResolveDefaultKeyBytes(brokerOptions.Value.DefaultEncryptionKeyBase64);
    }

    public async Task<IReadOnlyCollection<byte[]>> ResolveCandidateKeysAsync(
        string workspaceId,
        string topic,
        CancellationToken cancellationToken = default)
    {
        await EnsureCacheIsFreshAsync(workspaceId, cancellationToken);

        var cache = GetWorkspaceCache(workspaceId);

        var bestCustomMatch = TryFindBestMatch(topic, cache.Mappings);
        var defaultKeyBytes = cache.DefaultKeyBytes;

        if (bestCustomMatch is null)
        {
            return [[..defaultKeyBytes]];
        }

        if (bestCustomMatch.KeyBytes.SequenceEqual(defaultKeyBytes))
        {
            return [[..defaultKeyBytes]];
        }

        return [[..bestCustomMatch.KeyBytes], [..defaultKeyBytes]];
    }

    public void InvalidateCache()
    {
        lock (_cacheSync)
        {
            _cacheByWorkspace.Clear();
        }
    }

    private async Task EnsureCacheIsFreshAsync(string workspaceId, CancellationToken cancellationToken)
    {
        if (IsCacheFresh(workspaceId))
        {
            return;
        }

        await _refreshLock.WaitAsync(cancellationToken);

        try
        {
            if (IsCacheFresh(workspaceId))
            {
                return;
            }

            await using var scope = _serviceScopeFactory.CreateAsyncScope();
            var topicPresetRepository = scope.ServiceProvider.GetRequiredService<ITopicPresetRepository>();
            var brokerServerProfileRepository = scope.ServiceProvider.GetRequiredService<IBrokerServerProfileRepository>();
            var activeServer = await brokerServerProfileRepository.GetActiveAsync(workspaceId, cancellationToken);
            var presets = activeServer is null
                ? Array.Empty<TopicPreset>()
                : await topicPresetRepository.GetAllAsync(workspaceId, activeServer.Id, cancellationToken);
            var defaultKeyBytes = ResolveDefaultKeyBytes(activeServer?.DefaultEncryptionKeyBase64 ?? string.Empty);

            var mappings = presets
                .Select(TryMapPresetToKeyMapping)
                .Where(mapping => mapping is not null)
                .Select(mapping => mapping!)
                .ToList();

            lock (_cacheSync)
            {
                _cacheByWorkspace[workspaceId] = new WorkspaceKeyCache
                {
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                    DefaultKeyBytes = defaultKeyBytes,
                    Mappings = mappings
                };
            }
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private bool IsCacheFresh(string workspaceId)
    {
        lock (_cacheSync)
        {
            return _cacheByWorkspace.TryGetValue(workspaceId, out var cache) &&
                DateTimeOffset.UtcNow - cache.UpdatedAtUtc < CacheLifetime;
        }
    }

    private WorkspaceKeyCache GetWorkspaceCache(string workspaceId)
    {
        lock (_cacheSync)
        {
            if (_cacheByWorkspace.TryGetValue(workspaceId, out var cache))
            {
                return cache;
            }
        }

        return new WorkspaceKeyCache
        {
            UpdatedAtUtc = DateTimeOffset.MinValue,
            DefaultKeyBytes = [.._fallbackDefaultKeyBytes],
            Mappings = []
        };
    }

    private KeyMapping? TryFindBestMatch(string topic, IReadOnlyCollection<KeyMapping> mappings)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return null;
        }

        var normalizedTopic = NormalizeTransportSegment(topic);
        KeyMapping? bestMatch = null;

        foreach (var mapping in mappings)
        {
            if (!IsMatch(normalizedTopic, mapping.NormalizedPattern))
            {
                continue;
            }

            if (bestMatch is null || mapping.MatchScore > bestMatch.MatchScore)
            {
                bestMatch = mapping;
            }
        }

        return bestMatch;
    }

    private KeyMapping? TryMapPresetToKeyMapping(TopicPreset preset)
    {
        if (string.IsNullOrWhiteSpace(preset.EncryptionKeyBase64))
        {
            return null;
        }

        if (!TopicEncryptionKey.TryParse(preset.EncryptionKeyBase64, out var keyBytes))
        {
            _logger.LogWarning(
                "Ignoring topic preset with invalid encryption key. Pattern: {TopicPattern}",
                preset.TopicPattern);
            return null;
        }

        var normalizedPattern = NormalizeTransportSegment(preset.TopicPattern);
        if (string.IsNullOrWhiteSpace(normalizedPattern))
        {
            return null;
        }

        return new KeyMapping
        {
            NormalizedPattern = normalizedPattern,
            MatchScore = CalculateMatchScore(normalizedPattern),
            KeyBytes = keyBytes
        };
    }

    private byte[] ResolveDefaultKeyBytes(string? configuredDefaultKeyBase64)
    {
        if (string.IsNullOrWhiteSpace(configuredDefaultKeyBase64))
        {
            return [..TopicEncryptionKey.DefaultKeyBytes];
        }

        if (TopicEncryptionKey.TryParse(configuredDefaultKeyBase64, out var configuredDefaultKey))
        {
            return configuredDefaultKey;
        }

        _logger.LogWarning(
            "Configured broker default encryption key is invalid. Falling back to the Meshtastic default key.");
        return [..TopicEncryptionKey.DefaultKeyBytes];
    }

    private static int CalculateMatchScore(string topicPattern)
    {
        var segments = topicPattern.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var score = 0;

        foreach (var segment in segments)
        {
            score += segment switch
            {
                "#" => 1,
                "+" => 3,
                _ => 10
            };
        }

        return score;
    }

    private static bool IsMatch(string topic, string pattern)
    {
        var topicSegments = topic.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var patternSegments = pattern.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (var index = 0; index < patternSegments.Length; index++)
        {
            var patternSegment = patternSegments[index];

            if (patternSegment == "#")
            {
                return true;
            }

            if (index >= topicSegments.Length)
            {
                return false;
            }

            if (patternSegment == "+")
            {
                continue;
            }

            if (!string.Equals(patternSegment, topicSegments[index], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return topicSegments.Length == patternSegments.Length;
    }

    private static string NormalizeTransportSegment(string topicPattern)
    {
        if (string.IsNullOrWhiteSpace(topicPattern))
        {
            return string.Empty;
        }

        var segments = topicPattern
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length >= 4 &&
            string.Equals(segments[0], "msh", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(segments[3], "json", StringComparison.OrdinalIgnoreCase))
        {
            segments[3] = "e";
        }

        return string.Join('/', segments);
    }

    private sealed class KeyMapping
    {
        public required byte[] KeyBytes { get; set; }

        public int MatchScore { get; set; }

        public required string NormalizedPattern { get; set; }
    }

    private sealed class WorkspaceKeyCache
    {
        public DateTimeOffset UpdatedAtUtc { get; set; }

        public required byte[] DefaultKeyBytes { get; set; }

        public required IReadOnlyCollection<KeyMapping> Mappings { get; set; }
    }
}
