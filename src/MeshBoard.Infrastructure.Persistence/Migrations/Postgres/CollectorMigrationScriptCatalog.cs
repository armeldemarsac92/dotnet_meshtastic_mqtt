using System.Reflection;

namespace MeshBoard.Infrastructure.Persistence.Migrations.Postgres;

internal static class CollectorMigrationScriptCatalog
{
    private const string ResourcePrefix = "MeshBoard.Infrastructure.Persistence.Migrations.Postgres.Collector.";

    public static IReadOnlyList<string> GetOrderedResourceNames()
    {
        return typeof(CollectorMigrationScriptCatalog).Assembly
            .GetManifestResourceNames()
            .Where(resourceName => resourceName.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .OrderBy(resourceName => resourceName, StringComparer.Ordinal)
            .ToList();
    }

    public static string ReadScript(string resourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);

        using var stream = typeof(CollectorMigrationScriptCatalog).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Migration script resource '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
