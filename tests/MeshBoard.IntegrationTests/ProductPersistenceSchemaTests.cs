using MeshBoard.Infrastructure.Persistence.DependencyInjection;

namespace MeshBoard.IntegrationTests;

public sealed class ProductPersistenceSchemaTests
{
    [Fact]
    public void PostgresPreferenceSchema_ShouldContainOnlyRetainedProductTables()
    {
        var persistenceAssembly = typeof(ServiceCollectionExtensions).Assembly;
        var resourcePrefix = "MeshBoard.Infrastructure.Persistence.Migrations.Postgres.Product.";
        var resourceNames = persistenceAssembly
            .GetManifestResourceNames()
            .Where(resourceName => resourceName.StartsWith(resourcePrefix, StringComparison.Ordinal))
            .OrderBy(resourceName => resourceName, StringComparer.Ordinal)
            .ToList();
        var createSchema = string.Join(
            Environment.NewLine,
            resourceNames.Select(
                resourceName =>
                {
                    using var stream = persistenceAssembly.GetManifestResourceStream(resourceName)
                        ?? throw new InvalidOperationException($"Migration script resource '{resourceName}' was not found.");
                    using var reader = new StreamReader(stream);
                    return reader.ReadToEnd();
                }));

        Assert.NotEmpty(resourceNames);
        Assert.Contains("CREATE TABLE IF NOT EXISTS users", createSchema, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS broker_server_profiles", createSchema, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS favorite_nodes", createSchema, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS topic_presets", createSchema, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS saved_channel_filters", createSchema, StringComparison.Ordinal);
        Assert.DoesNotContain("default_encryption_key_base64", createSchema, StringComparison.Ordinal);
        Assert.DoesNotContain("encryption_key_base64", createSchema, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE IF NOT EXISTS message_history", createSchema, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE IF NOT EXISTS nodes", createSchema, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE IF NOT EXISTS workspace_runtime_status", createSchema, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE IF NOT EXISTS broker_runtime_commands", createSchema, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE IF NOT EXISTS projection_change_log", createSchema, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE IF NOT EXISTS runtime_pipeline_status", createSchema, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE IF NOT EXISTS subscription_intents", createSchema, StringComparison.Ordinal);
    }
}
