using System.Diagnostics;
using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Application.Services;
using MeshBoard.Collector.StatsProjector.Observability;
using MeshBoard.Contracts.CollectorEvents.Normalized;
using MeshBoard.Contracts.Messages;

namespace MeshBoard.Collector.StatsProjector.Services;

public sealed class StatsPacketProjectionService : IStatsPacketProjectionService
{
    private readonly IMessageRepository _messageRepository;
    private readonly ITopicDiscoveryService _topicDiscoveryService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StatsPacketProjectionService> _logger;

    public StatsPacketProjectionService(
        IMessageRepository messageRepository,
        ITopicDiscoveryService topicDiscoveryService,
        IUnitOfWork unitOfWork,
        ILogger<StatsPacketProjectionService> logger)
    {
        _messageRepository = messageRepository;
        _topicDiscoveryService = topicDiscoveryService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task ProjectAsync(PacketNormalized packet, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(packet);
        var startedAt = Stopwatch.GetTimestamp();

        _logger.LogDebug(
            "Projecting packet key {PacketKey} from broker {BrokerServer} topic {Topic}",
            packet.PacketKey,
            packet.BrokerServer,
            packet.Topic);

        await _unitOfWork.BeginTransactionAsync(ct);

        try
        {
            var messageInserted = await _messageRepository.AddAsync(
                new SaveObservedMessageRequest
                {
                    BrokerServer = packet.BrokerServer,
                    Topic = packet.Topic,
                    PacketType = packet.PacketType,
                    MessageKey = packet.PacketKey,
                    FromNodeId = packet.FromNodeId ?? "unknown",
                    ToNodeId = packet.ToNodeId,
                    PayloadPreview = packet.PayloadPreview,
                    IsPrivate = packet.IsPrivate,
                    ReceivedAtUtc = packet.ReceivedAtUtc
                },
                ct);

            if (!messageInserted)
            {
                _logger.LogDebug(
                    "Skipping duplicate packet projection for message key {MessageKey}",
                    packet.PacketKey);

                await _unitOfWork.CommitAsync(ct);
                StatsProjectorObservability.RecordTransactionCompleted(
                    Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
                return;
            }

            await _topicDiscoveryService.RecordObservedTopic(
                packet.Topic,
                packet.ReceivedAtUtc,
                packet.BrokerServer,
                string.Empty,
                ct);

            await _unitOfWork.CommitAsync(ct);
            StatsProjectorObservability.RecordPacketProjected();
            StatsProjectorObservability.RecordTransactionCompleted(
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            throw;
        }
    }
}
