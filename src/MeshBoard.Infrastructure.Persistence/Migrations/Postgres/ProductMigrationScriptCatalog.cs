using System.Reflection;

namespace MeshBoard.Infrastructure.Persistence.Migrations.Postgres;

internal static class ProductMigrationScriptCatalog
{
    private const string ResourcePrefix = "MeshBoard.Infrastructure.Persistence.Migrations.Postgres.Product.";

    public static IReadOnlyList<string> GetOrderedResourceNames()
    {
        return typeof(ProductMigrationScriptCatalog).Assembly
            .GetManifestResourceNames()
            .Where(resourceName => resourceName.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .OrderBy(resourceName => resourceName, StringComparer.Ordinal)
            .ToList();
    }

    public static string ReadScript(string resourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);

        using var stream = typeof(ProductMigrationScriptCatalog).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Migration script resource '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
