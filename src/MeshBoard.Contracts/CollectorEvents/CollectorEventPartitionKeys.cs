namespace MeshBoard.Contracts.CollectorEvents;

public static class CollectorEventPartitionKeys
{
    public static string BuildChannelScope(
        string brokerServer,
        string topicPattern)
    {
        ValidateSegment(brokerServer, nameof(brokerServer));
        ValidateSegment(topicPattern, nameof(topicPattern));

        return $"{brokerServer}|{topicPattern}";
    }

    private static void ValidateSegment(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty partition key segment is required.", parameterName);
        }

        if (value.Contains('|', StringComparison.Ordinal))
        {
            throw new ArgumentException("Partition key segments may not contain the '|' separator.", parameterName);
        }
    }
}
