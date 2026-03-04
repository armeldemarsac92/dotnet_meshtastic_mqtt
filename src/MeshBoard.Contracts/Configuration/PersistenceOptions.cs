namespace MeshBoard.Contracts.Configuration;

public sealed class PersistenceOptions
{
    public const string SectionName = "Persistence";

    public string Provider { get; set; } = "SQLite";

    public string ConnectionString { get; set; } = "Data Source=meshboard.db";

    public int MessageRetentionDays { get; set; } = 30;
}
