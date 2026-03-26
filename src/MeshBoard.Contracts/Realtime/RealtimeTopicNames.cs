namespace MeshBoard.Contracts.Realtime;

public static class RealtimeTopicNames
{
    public static string BuildWorkspaceLiveWildcard(string workspaceId)
    {
        var normalizedWorkspaceId = NormalizeTopicSegment(workspaceId, nameof(workspaceId));
        return $"meshboard/workspaces/{normalizedWorkspaceId}/live/#";
    }

    public static string BuildWorkspacePacketTopic(string workspaceId)
    {
        var normalizedWorkspaceId = NormalizeTopicSegment(workspaceId, nameof(workspaceId));
        return $"meshboard/workspaces/{normalizedWorkspaceId}/live/packets";
    }

    private static string NormalizeTopicSegment(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A topic segment is required.", parameterName);
        }

        var normalized = value.Trim();
        if (normalized.Contains('/') || normalized.Contains('+') || normalized.Contains('#'))
        {
            throw new ArgumentException(
                "Topic segments must not contain '/', '+', or '#'.",
                parameterName);
        }

        return normalized;
    }
}
