using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Messages;
using MeshBoard.Contracts.Meshtastic;
using MeshBoard.Contracts.Nodes;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace MeshBoard.Application.Services;

public interface IMeshtasticIngestionService
{
    Task IngestEnvelope(MeshtasticEnvelope envelope, CancellationToken cancellationToken = default);
}

public sealed class MeshtasticIngestionService : IMeshtasticIngestionService
{
    private readonly ILogger<MeshtasticIngestionService> _logger;
    private readonly IMessageRepository _messageRepository;
    private readonly INeighborLinkRepository? _neighborLinkRepository;
    private readonly INodeRepository _nodeRepository;
    private readonly ITopicDiscoveryService _topicDiscoveryService;
    private readonly IUnitOfWork _unitOfWork;

    public MeshtasticIngestionService(
        IMessageRepository messageRepository,
        INodeRepository nodeRepository,
        ITopicDiscoveryService topicDiscoveryService,
        IUnitOfWork unitOfWork,
        ILogger<MeshtasticIngestionService> logger,
        INeighborLinkRepository? neighborLinkRepository = null)
    {
        _messageRepository = messageRepository;
        _neighborLinkRepository = neighborLinkRepository;
        _nodeRepository = nodeRepository;
        _topicDiscoveryService = topicDiscoveryService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task IngestEnvelope(MeshtasticEnvelope envelope, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Attempting to ingest Meshtastic envelope from topic: {Topic}", envelope.Topic);
        var messageKey = BuildMessageKey(envelope);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var messageInserted = await _messageRepository.AddAsync(
                new SaveObservedMessageRequest
                {
                    BrokerServer = envelope.BrokerServer,
                    Topic = envelope.Topic,
                    PacketType = envelope.PacketType,
                    MessageKey = messageKey,
                    FromNodeId = envelope.FromNodeId ?? "unknown",
                    ToNodeId = envelope.ToNodeId,
                    PayloadPreview = envelope.PayloadPreview,
                    IsPrivate = envelope.IsPrivate,
                    ReceivedAtUtc = envelope.ReceivedAtUtc
                },
                cancellationToken);

            if (!messageInserted)
            {
                _logger.LogDebug(
                    "Skipping duplicate Meshtastic packet for message key {MessageKey} from node {FromNodeId}",
                    messageKey,
                    envelope.FromNodeId);

                await _unitOfWork.CommitAsync(cancellationToken);
                return;
            }

            await _topicDiscoveryService.RecordObservedTopic(
                envelope.Topic,
                envelope.ReceivedAtUtc,
                envelope.BrokerServer,
                string.Empty,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(envelope.FromNodeId))
            {
                await _nodeRepository.UpsertAsync(
                    new UpsertObservedNodeRequest
                    {
                        NodeId = envelope.FromNodeId,
                        BrokerServer = envelope.BrokerServer,
                        ShortName = envelope.ShortName,
                        LongName = envelope.LongName,
                        LastHeardAtUtc = envelope.ReceivedAtUtc,
                        LastHeardChannel = envelope.LastHeardChannel,
                        LastTextMessageAtUtc = envelope.PacketType == "Text Message"
                            ? envelope.ReceivedAtUtc
                            : null,
                        LastKnownLatitude = envelope.Latitude,
                        LastKnownLongitude = envelope.Longitude,
                        BatteryLevelPercent = envelope.BatteryLevelPercent,
                        Voltage = envelope.Voltage,
                        ChannelUtilization = envelope.ChannelUtilization,
                        AirUtilTx = envelope.AirUtilTx,
                        UptimeSeconds = envelope.UptimeSeconds,
                        TemperatureCelsius = envelope.TemperatureCelsius,
                        RelativeHumidity = envelope.RelativeHumidity,
                        BarometricPressure = envelope.BarometricPressure
                    },
                    cancellationToken);
            }

            if (_neighborLinkRepository is not null)
            {
                var neighborLinks = BuildNeighborLinkRecords(envelope);

                if (neighborLinks.Count > 0)
                {
                    await _neighborLinkRepository.UpsertAsync(
                        envelope.BrokerServer,
                        envelope.LastHeardChannel,
                        neighborLinks,
                        cancellationToken);
                }
            }

            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static string BuildMessageKey(MeshtasticEnvelope envelope)
    {
        if (envelope.PacketId.HasValue && !string.IsNullOrWhiteSpace(envelope.FromNodeId))
        {
            return $"{envelope.BrokerServer}|{envelope.FromNodeId}:{envelope.PacketId.Value:x8}";
        }

        var rawKey =
            $"{envelope.BrokerServer}|{envelope.PacketType}|{envelope.FromNodeId}|{envelope.ToNodeId}|{envelope.PayloadPreview}|{envelope.ReceivedAtUtc:O}";

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToHexStringLower(hashBytes);
    }

    private static IReadOnlyList<NeighborLinkRecord> BuildNeighborLinkRecords(MeshtasticEnvelope envelope)
    {
        if (string.IsNullOrWhiteSpace(envelope.FromNodeId) || envelope.Neighbors is null || envelope.Neighbors.Count == 0)
        {
            return [];
        }

        var reportingNodeId = envelope.FromNodeId.Trim();
        var linksByKey = new Dictionary<string, NeighborLinkRecord>(StringComparer.OrdinalIgnoreCase);

        foreach (var neighbor in envelope.Neighbors)
        {
            if (string.IsNullOrWhiteSpace(neighbor.NodeId) ||
                string.Equals(reportingNodeId, neighbor.NodeId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var canonical = CreateCanonicalNeighborLink(
                reportingNodeId,
                neighbor.NodeId,
                neighbor.SnrDb,
                neighbor.LastRxAtUtc ?? envelope.ReceivedAtUtc);
            var linkKey = $"{canonical.SourceNodeId}|{canonical.TargetNodeId}";

            if (!linksByKey.TryGetValue(linkKey, out var existing))
            {
                linksByKey[linkKey] = canonical;
                continue;
            }

            var isIncomingLatest = canonical.LastSeenAtUtc >= existing.LastSeenAtUtc;
            linksByKey[linkKey] = new NeighborLinkRecord
            {
                SourceNodeId = existing.SourceNodeId,
                TargetNodeId = existing.TargetNodeId,
                SnrDb = isIncomingLatest
                    ? canonical.SnrDb ?? existing.SnrDb
                    : existing.SnrDb ?? canonical.SnrDb,
                LastSeenAtUtc = isIncomingLatest
                    ? canonical.LastSeenAtUtc
                    : existing.LastSeenAtUtc
            };
        }

        return linksByKey.Values.ToArray();
    }

    private static NeighborLinkRecord CreateCanonicalNeighborLink(
        string leftNodeId,
        string rightNodeId,
        float? snrDb,
        DateTimeOffset lastSeenAtUtc)
    {
        var normalizedLeft = leftNodeId.Trim();
        var normalizedRight = rightNodeId.Trim();
        var leftFirst = StringComparer.OrdinalIgnoreCase.Compare(normalizedLeft, normalizedRight) <= 0;

        return new NeighborLinkRecord
        {
            SourceNodeId = leftFirst ? normalizedLeft : normalizedRight,
            TargetNodeId = leftFirst ? normalizedRight : normalizedLeft,
            SnrDb = snrDb,
            LastSeenAtUtc = lastSeenAtUtc
        };
    }
}
