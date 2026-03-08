using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Messages;
using MeshBoard.Contracts.Meshtastic;
using MeshBoard.Contracts.Nodes;
using MeshBoard.Contracts.Realtime;
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
    private readonly INodeRepository _nodeRepository;
    private readonly IProjectionChangeRepository _projectionChangeRepository;
    private readonly ITopicDiscoveryService _topicDiscoveryService;
    private readonly IUnitOfWork _unitOfWork;

    public MeshtasticIngestionService(
        IMessageRepository messageRepository,
        INodeRepository nodeRepository,
        IProjectionChangeRepository projectionChangeRepository,
        ITopicDiscoveryService topicDiscoveryService,
        IUnitOfWork unitOfWork,
        ILogger<MeshtasticIngestionService> logger)
    {
        _messageRepository = messageRepository;
        _nodeRepository = nodeRepository;
        _projectionChangeRepository = projectionChangeRepository;
        _topicDiscoveryService = topicDiscoveryService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task IngestEnvelope(MeshtasticEnvelope envelope, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Attempting to ingest Meshtastic envelope from topic: {Topic}", envelope.Topic);
        var workspaceId = RequireWorkspaceId(envelope);
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

            await _unitOfWork.CommitAsync(cancellationToken);

            await PublishProjectionChangesAsync(workspaceId, envelope, cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task PublishProjectionChangesAsync(
        string workspaceId,
        MeshtasticEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var changes = new List<ProjectionChangeDescriptor>
        {
            new()
            {
                Kind = ProjectionChangeKind.MessageAdded
            },
            new()
            {
                Kind = ProjectionChangeKind.ChannelSummaryUpdated,
                EntityKey = ResolveChannelEntityKey(envelope)
            }
        };

        if (!string.IsNullOrWhiteSpace(envelope.FromNodeId))
        {
            changes.Add(
                new ProjectionChangeDescriptor
                {
                    Kind = ProjectionChangeKind.NodeUpdated,
                    EntityKey = envelope.FromNodeId.Trim()
                });
        }

        try
        {
            await _projectionChangeRepository.AppendAsync(workspaceId, changes, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(
                exception,
                "Failed to persist projection change notifications for workspace {WorkspaceId} after ingesting topic {Topic}",
                workspaceId,
                envelope.Topic);
        }
    }

    private static string RequireWorkspaceId(MeshtasticEnvelope envelope)
    {
        if (string.IsNullOrWhiteSpace(envelope.WorkspaceId))
        {
            throw new InvalidOperationException("A workspace ID is required to ingest Meshtastic envelopes.");
        }

        return envelope.WorkspaceId.Trim();
    }

    private static string? ResolveChannelEntityKey(MeshtasticEnvelope envelope)
    {
        if (!string.IsNullOrWhiteSpace(envelope.LastHeardChannel))
        {
            return envelope.LastHeardChannel.Trim();
        }

        if (string.IsNullOrWhiteSpace(envelope.Topic))
        {
            return null;
        }

        var segments = envelope.Topic
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length < 5 ||
            !string.Equals(segments[0], "msh", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var region = segments[1];
        var channel = segments[4];

        if (string.IsNullOrWhiteSpace(region) ||
            string.IsNullOrWhiteSpace(channel) ||
            channel is "#" or "+")
        {
            return null;
        }

        return $"{region}/{channel}";
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
}
