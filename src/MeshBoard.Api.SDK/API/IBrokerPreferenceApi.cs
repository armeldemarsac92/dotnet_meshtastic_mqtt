using MeshBoard.Contracts.Api;
using MeshBoard.Contracts.Configuration;
using Refit;

namespace MeshBoard.Api.SDK.API;

public interface IBrokerPreferenceApi
{
    [Get(ApiRoutes.Preferences.Brokers.GetAll)]
    Task<IApiResponse<List<SavedBrokerServerProfile>>> GetAllAsync(CancellationToken cancellationToken = default);

    [Get(ApiRoutes.Preferences.Brokers.GetActive)]
    Task<IApiResponse<SavedBrokerServerProfile>> GetActiveAsync(CancellationToken cancellationToken = default);

    [Post(ApiRoutes.Preferences.Brokers.Create)]
    Task<IApiResponse<SavedBrokerServerProfile>> CreateAsync([Body] SaveBrokerPreferenceRequest request, CancellationToken cancellationToken = default);

    [Put(ApiRoutes.Preferences.Brokers.Update)]
    Task<IApiResponse<SavedBrokerServerProfile>> UpdateAsync(Guid id, [Body] SaveBrokerPreferenceRequest request, CancellationToken cancellationToken = default);

    [Post(ApiRoutes.Preferences.Brokers.Activate)]
    Task<IApiResponse<SavedBrokerServerProfile>> ActivateAsync(Guid id, CancellationToken cancellationToken = default);
}
