namespace MeshBoard.Client.Vault;

public interface IVaultRuntimeKeyRecordProvider
{
    Task<IReadOnlyList<LocalVaultRuntimeKeyRecord>> GetRuntimeKeyRecordsAsync(CancellationToken cancellationToken = default);
}
