using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Contracts.Meshtastic;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Infrastructure.Meshtastic.Bootstrap;

internal sealed class NullMeshtasticEnvelopeReader : IMeshtasticEnvelopeReader
{
    private readonly ILogger<NullMeshtasticEnvelopeReader> _logger;

    public NullMeshtasticEnvelopeReader(ILogger<NullMeshtasticEnvelopeReader> logger)
    {
        _logger = logger;
    }

    public Task<MeshtasticEnvelope?> Read(
        string topic,
        byte[] payload,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Meshtastic envelope read requested before protocol integration is implemented for topic: {Topic}",
            topic);

        MeshtasticEnvelope envelope = new()
        {
            Topic = topic,
            PacketType = "Raw MQTT Packet",
            PayloadPreview = $"Raw payload ({payload.Length} bytes)",
            ReceivedAtUtc = DateTimeOffset.UtcNow
        };

        return Task.FromResult<MeshtasticEnvelope?>(envelope);
    }
}
