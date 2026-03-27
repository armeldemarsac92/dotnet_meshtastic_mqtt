using MeshBoard.Contracts.Meshtastic;

namespace MeshBoard.Application.Collector;

public interface ILinkDerivationService
{
    IReadOnlyList<NeighborLinkRecord> DeriveLinks(MeshtasticEnvelope envelope);
}
