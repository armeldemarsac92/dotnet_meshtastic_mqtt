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
    private readonly INodeRepository _nodeRepository;
    private readonly IUnitOfWork _unitOfWork;

    public MeshtasticIngestionService(
        IMessageRepository messageRepository,
        INodeRepository nodeRepository,
        IUnitOfWork unitOfWork,
        ILogger<MeshtasticIngestionService> logger)
    {
        _messageRepository = messageRepository;
        _nodeRepository = nodeRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task IngestEnvelope(MeshtasticEnvelope envelope, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Attempting to ingest Meshtastic envelope from topic: {Topic}", envelope.Topic);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var messageInserted = await _messageRepository.AddAsync(
                new SaveObservedMessageRequest
                {
                    Topic = envelope.Topic,
                    PacketType = envelope.PacketType,
                    MessageKey = BuildMessageKey(envelope),
                    FromNodeId = envelope.FromNodeId ?? "unknown",
                    ToNodeId = envelope.ToNodeId,
                    PayloadPreview = envelope.PayloadPreview,
                    IsPrivate = envelope.IsPrivate,
                    ReceivedAtUtc = envelope.ReceivedAtUtc
                },
                cancellationToken);

            if (!messageInserted)
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                return;
            }

            if (!string.IsNullOrWhiteSpace(envelope.FromNodeId))
            {
                await _nodeRepository.UpsertAsync(
                    new UpsertObservedNodeRequest
                    {
                        NodeId = envelope.FromNodeId,
                        ShortName = envelope.ShortName,
                        LongName = envelope.LongName,
                        LastHeardAtUtc = envelope.ReceivedAtUtc,
                        LastTextMessageAtUtc = envelope.PacketType == "Text Message"
                            ? envelope.ReceivedAtUtc
                            : null,
                        LastKnownLatitude = envelope.Latitude,
                        LastKnownLongitude = envelope.Longitude
                    },
                    cancellationToken);
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
            return $"{envelope.FromNodeId}:{envelope.PacketId.Value:x8}";
        }

        var rawKey =
            $"{envelope.PacketType}|{envelope.FromNodeId}|{envelope.ToNodeId}|{envelope.PayloadPreview}|{envelope.ReceivedAtUtc:O}";

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToHexStringLower(hashBytes);
    }
}
