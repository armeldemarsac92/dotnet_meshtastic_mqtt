using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Application.Collector;
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
    private readonly ILinkDerivationService _linkDerivationService;
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
        ILinkDerivationService linkDerivationService,
        ILogger<MeshtasticIngestionService> logger,
        INeighborLinkRepository? neighborLinkRepository = null)
    {
        _linkDerivationService = linkDerivationService;
        _messageRepository = messageRepository;
        _neighborLinkRepository = neighborLinkRepository;
        _nodeRepository = nodeRepository;
        _topicDiscoveryService = topicDiscoveryService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public MeshtasticIngestionService(
        IMessageRepository messageRepository,
        INodeRepository nodeRepository,
        ITopicDiscoveryService topicDiscoveryService,
        IUnitOfWork unitOfWork,
        ILogger<MeshtasticIngestionService> logger,
        INeighborLinkRepository? neighborLinkRepository = null)
        : this(
            messageRepository,
            nodeRepository,
            topicDiscoveryService,
            unitOfWork,
            new LinkDerivationService(),
            logger,
            neighborLinkRepository)
    {
    }

    public async Task IngestEnvelope(MeshtasticEnvelope envelope, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Attempting to ingest Meshtastic envelope from topic: {Topic}", envelope.Topic);
        var messageKey = BuildMessageKey(envelope);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var messageInserted = await _messageRepository.AddAsync(
                envelope.ToSaveObservedMessageRequest(messageKey),
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
                    envelope.ToUpsertObservedNodeRequest(),
                    cancellationToken);
            }

            if (_neighborLinkRepository is not null)
            {
                var allLinks = _linkDerivationService.DeriveLinks(envelope);

                if (allLinks.Count > 0)
                {
                    await _neighborLinkRepository.UpsertAsync(
                        envelope.BrokerServer,
                        envelope.LastHeardChannel,
                        allLinks,
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
}
