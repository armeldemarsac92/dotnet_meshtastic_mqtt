using MeshBoard.Contracts.Meshtastic;

namespace MeshBoard.Application.Abstractions.Meshtastic;

public interface IMeshtasticEnvelopeReader
{
    Task<MeshtasticEnvelope?> Read(string topic, byte[] payload, CancellationToken cancellationToken = default);
}
