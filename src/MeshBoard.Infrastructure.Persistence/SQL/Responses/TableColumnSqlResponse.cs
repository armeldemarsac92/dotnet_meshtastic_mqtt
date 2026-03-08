namespace MeshBoard.Infrastructure.Persistence.SQL.Responses;

internal sealed class TableColumnSqlResponse
{
    public required string Name { get; set; }

    public int Pk { get; set; }
}
