namespace MeshBoard.Infrastructure.Persistence.Initialization;

internal interface IPersistenceInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
