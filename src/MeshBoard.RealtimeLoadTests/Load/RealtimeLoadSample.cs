namespace MeshBoard.RealtimeLoadTests.Load;

public sealed record RealtimeLoadSample(
    string Operation,
    bool IsSuccess,
    double DurationMs,
    string? FailureReason = null);
