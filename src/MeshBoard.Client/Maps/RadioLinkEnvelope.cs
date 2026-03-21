namespace MeshBoard.Client.Maps;

public sealed record RadioLinkEnvelope(
    string SourceNodeId,
    string TargetNodeId,
    float? SnrDb,
    DateTimeOffset LastSeenAtUtc);
