using MeshBoard.Contracts.Configuration;
using Refit;

namespace MeshBoard.Api.SDK.API;

public interface IBrokerPreferenceApi
{
    [Get("/api/preferences/brokers")]
    Task<IApiResponse<List<SavedBrokerServerProfile>>> GetAllAsync(CancellationToken cancellationToken = default);

    [Get("/api/preferences/brokers/active")]
    Task<IApiResponse<SavedBrokerServerProfile>> GetActiveAsync(CancellationToken cancellationToken = default);

    [Post("/api/preferences/brokers")]
    Task<IApiResponse<SavedBrokerServerProfile>> CreateAsync([Body] SaveBrokerPreferenceRequest request, CancellationToken cancellationToken = default);

    [Put("/api/preferences/brokers/{id}")]
    Task<IApiResponse<SavedBrokerServerProfile>> UpdateAsync(Guid id, [Body] SaveBrokerPreferenceRequest request, CancellationToken cancellationToken = default);

    [Post("/api/preferences/brokers/{id}/activate")]
    Task<IApiResponse<SavedBrokerServerProfile>> ActivateAsync(Guid id, CancellationToken cancellationToken = default);
}
