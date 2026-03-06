namespace MeshBoard.Infrastructure.Persistence.SQL.Requests;

internal sealed class UpsertBrokerServerProfileSqlRequest
{
    public required string Id { get; set; }

    public required string Name { get; set; }

    public required string Host { get; set; }

    public int Port { get; set; }

    public int UseTls { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public required string DefaultTopicPattern { get; set; }

    public required string DefaultEncryptionKeyBase64 { get; set; }

    public required string DownlinkTopic { get; set; }

    public int EnableSend { get; set; }

    public int IsActive { get; set; }

    public required string CreatedAtUtc { get; set; }
}
