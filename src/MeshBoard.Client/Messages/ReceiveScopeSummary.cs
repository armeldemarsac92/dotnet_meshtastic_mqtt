namespace MeshBoard.Client.Messages;

public sealed class ReceiveScopeSummary
{
    public static ReceiveScopeSummary Empty { get; } = new();

    public bool HasActiveServer { get; init; }

    public string ServerName { get; init; } = string.Empty;

    public string ServerAddress { get; init; } = string.Empty;

    public bool UsesFallbackTopic { get; init; }

    public IReadOnlyList<ReceiveScopeTopic> Topics { get; init; } = [];
}
