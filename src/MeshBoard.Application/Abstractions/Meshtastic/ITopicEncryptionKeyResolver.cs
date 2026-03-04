namespace MeshBoard.Application.Abstractions.Meshtastic;

public interface ITopicEncryptionKeyResolver
{
    Task<IReadOnlyCollection<byte[]>> ResolveCandidateKeysAsync(
        string topic,
        CancellationToken cancellationToken = default);

    void InvalidateCache();
}
