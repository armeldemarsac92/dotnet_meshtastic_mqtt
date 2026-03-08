namespace MeshBoard.Contracts.Configuration;

public sealed class MeshtasticRuntimeOptions
{
    public const string SectionName = "MeshtasticRuntime";

    public bool EnableHostedService { get; set; } = true;
}
