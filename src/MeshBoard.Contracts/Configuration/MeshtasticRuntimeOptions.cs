namespace MeshBoard.Contracts.Configuration;

public sealed class MeshtasticRuntimeOptions
{
    public const string SectionName = "MeshtasticRuntime";

    public bool EnableHostedService { get; set; } = true;

    public int CommandProcessorPollIntervalMilliseconds { get; set; } = 250;

    public int CommandProcessorBatchSize { get; set; } = 32;

    public int CommandLeaseDurationSeconds { get; set; } = 30;

    public int CommandRetryDelayMilliseconds { get; set; } = 1000;

    public int CommandMaxAttempts { get; set; } = 3;
}
