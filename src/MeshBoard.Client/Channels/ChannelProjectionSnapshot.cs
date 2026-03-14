namespace MeshBoard.Client.Channels;

public sealed record ChannelProjectionSnapshot
{
    public static readonly IReadOnlyList<ChannelProjectionEnvelope> EmptyChannels = Array.Empty<ChannelProjectionEnvelope>();

    public IReadOnlyList<ChannelProjectionEnvelope> Channels { get; init; } = EmptyChannels;

    public DateTimeOffset? LastProjectedAtUtc { get; init; }

    public long TotalProjected { get; init; }
}
