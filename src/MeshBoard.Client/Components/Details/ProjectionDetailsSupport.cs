using System.Globalization;

namespace MeshBoard.Client.Components.Details;

internal static class ProjectionDetailsSupport
{
    public static string? FormatNodeId(uint? nodeNumber)
    {
        return nodeNumber.HasValue && nodeNumber.Value != 0
            ? $"!{nodeNumber.Value:x8}"
            : null;
    }

    public static bool TryParseNodeNumber(string? nodeId, out uint nodeNumber)
    {
        nodeNumber = 0;

        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return false;
        }

        var normalizedNodeId = nodeId.Trim();
        if (normalizedNodeId.StartsWith('!'))
        {
            normalizedNodeId = normalizedNodeId[1..];
        }

        return uint.TryParse(
            normalizedNodeId,
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture,
            out nodeNumber) && nodeNumber != 0;
    }

    public static bool TryParseChannelKey(string? channelKey, out string region, out string channelName)
    {
        region = string.Empty;
        channelName = string.Empty;

        if (string.IsNullOrWhiteSpace(channelKey))
        {
            return false;
        }

        var segments = channelKey
            .Split('/', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length != 2 ||
            string.IsNullOrWhiteSpace(segments[0]) ||
            string.IsNullOrWhiteSpace(segments[1]))
        {
            return false;
        }

        region = segments[0];
        channelName = segments[1];
        return true;
    }

    public static bool IsTopicInChannel(string? topic, string region, string channelName)
    {
        if (string.IsNullOrWhiteSpace(topic) ||
            string.IsNullOrWhiteSpace(region) ||
            string.IsNullOrWhiteSpace(channelName))
        {
            return false;
        }

        var segments = topic
            .Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length < 5 || !string.Equals(segments[0], "msh", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var topicType = segments[3];
        if (!string.Equals(topicType, "e", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(topicType, "json", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(segments[1], region, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(segments[4], channelName, StringComparison.OrdinalIgnoreCase);
    }

    public static string BuildTopicFilter(string region, string channelName, string? sourceTopic = null)
    {
        var transportSegment = ResolveTransportSegment(sourceTopic) ?? "e";
        return $"msh/{region}/2/{transportSegment}/{channelName}/#";
    }

    private static string? ResolveTransportSegment(string? topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return null;
        }

        var segments = topic
            .Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length < 4 || !string.Equals(segments[0], "msh", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var transportSegment = segments[3];
        return string.Equals(transportSegment, "e", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(transportSegment, "json", StringComparison.OrdinalIgnoreCase)
                ? transportSegment
                : null;
    }
}
