using System.Text.Json;
using MeshBoard.RealtimeLoadTests.Configuration;

namespace MeshBoard.RealtimeLoadTests.Load;

internal sealed class RealtimeLoadReportWriter
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly RealtimeLoadTestOptions _options;

    public RealtimeLoadReportWriter(RealtimeLoadTestOptions options)
    {
        _options = options;
    }

    public async Task<string> WriteAsync(RealtimeLoadRunSummary summary, CancellationToken cancellationToken = default)
    {
        var outputDirectory = Path.GetFullPath(_options.ReportOutputDirectory);
        Directory.CreateDirectory(outputDirectory);

        var fileName = $"{summary.Scenario.ToConfigValue()}-{summary.StartedAtUtc:yyyyMMdd-HHmmss}.json";
        var outputPath = Path.Combine(outputDirectory, fileName);

        await File.WriteAllTextAsync(
            outputPath,
            JsonSerializer.Serialize(summary, JsonSerializerOptions),
            cancellationToken);

        return outputPath;
    }
}
