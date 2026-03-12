using MeshBoard.Contracts.Configuration;
using Refit;

namespace MeshBoard.Api.SDK.API;

public interface IBrokerPreferenceApi
{
    [Get("/api/preferences/brokers")]
    Task<IApiResponse<List<SavedBrokerServerProfile>>> GetAllAsync(CancellationToken cancellationToken = default);

    [Get("/api/preferences/brokers/active")]
    Task<IApiResponse<SavedBrokerServerProfile>> GetActiveAsync(CancellationToken cancellationToken = default);
}
