namespace MeshBoard.Contracts.Configuration;

public sealed class PersistenceOptions
{
    public const string SectionName = "Persistence";

    public string Provider { get; set; } = "PostgreSQL";

    public string ConnectionString { get; set; } =
        "Host=localhost;Port=15432;Database=meshboard;Username=meshboard;Password=meshboard";

    public int MessageRetentionDays { get; set; } = 30;
}
