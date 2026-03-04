namespace MeshBoard.Contracts.Topics;

public sealed class TopicProbeResult
{
    public int DurationSeconds { get; set; }

    public string SearchText { get; set; } = string.Empty;

    public IReadOnlyCollection<string> ProbedTopicFilters { get; set; } = [];

    public IReadOnlyCollection<string> TemporaryTopicFilters { get; set; } = [];
}
