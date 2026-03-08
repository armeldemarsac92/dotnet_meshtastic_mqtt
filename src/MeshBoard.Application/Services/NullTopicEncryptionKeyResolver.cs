using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Contracts.Topics;

namespace MeshBoard.Application.Services;

internal sealed class NullTopicEncryptionKeyResolver : ITopicEncryptionKeyResolver
{
    public void InvalidateCache()
    {
    }

    public Task<IReadOnlyCollection<byte[]>> ResolveCandidateKeysAsync(
        string workspaceId,
        string topic,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyCollection<byte[]>>
        (
            [
                TopicEncryptionKey.DefaultKeyBytes
            ]
        );
    }
}
