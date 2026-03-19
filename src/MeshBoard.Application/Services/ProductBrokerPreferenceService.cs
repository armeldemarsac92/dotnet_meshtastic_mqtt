using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Exceptions;

namespace MeshBoard.Application.Services;

public interface IProductBrokerPreferenceService
{
    Task<IReadOnlyCollection<SavedBrokerServerProfile>> GetBrokerPreferences(CancellationToken cancellationToken = default);

    Task<SavedBrokerServerProfile> GetActiveBrokerPreference(CancellationToken cancellationToken = default);

    Task<SavedBrokerServerProfile?> GetBrokerPreferenceById(Guid profileId, CancellationToken cancellationToken = default);

    Task<SavedBrokerServerProfile> CreateBrokerPreference(
        SaveBrokerPreferenceRequest request,
        CancellationToken cancellationToken = default);

    Task<SavedBrokerServerProfile> UpdateBrokerPreference(
        Guid profileId,
        SaveBrokerPreferenceRequest request,
        CancellationToken cancellationToken = default);

    Task<SavedBrokerServerProfile> ActivateBrokerPreference(Guid profileId, CancellationToken cancellationToken = default);
}

public sealed class ProductBrokerPreferenceService : IProductBrokerPreferenceService
{
    private readonly IBrokerServerProfileService _brokerServerProfileService;

    public ProductBrokerPreferenceService(IBrokerServerProfileService brokerServerProfileService)
    {
        _brokerServerProfileService = brokerServerProfileService;
    }

    public async Task<IReadOnlyCollection<SavedBrokerServerProfile>> GetBrokerPreferences(
        CancellationToken cancellationToken = default)
    {
        var profiles = await _brokerServerProfileService.GetServerProfiles(cancellationToken);
        return profiles.Select(profile => profile.ToSavedBrokerServerProfile()).ToList();
    }

    public async Task<SavedBrokerServerProfile> GetActiveBrokerPreference(CancellationToken cancellationToken = default)
    {
        var profile = await _brokerServerProfileService.GetActiveServerProfile(cancellationToken);
        return profile.ToSavedBrokerServerProfile();
    }

    public async Task<SavedBrokerServerProfile?> GetBrokerPreferenceById(
        Guid profileId,
        CancellationToken cancellationToken = default)
    {
        var profile = await _brokerServerProfileService.GetServerProfileById(profileId, cancellationToken);
        return profile?.ToSavedBrokerServerProfile();
    }

    public async Task<SavedBrokerServerProfile> CreateBrokerPreference(
        SaveBrokerPreferenceRequest request,
        CancellationToken cancellationToken = default)
    {
        var profile = await _brokerServerProfileService.SaveServerProfile(
            request.ToSaveBrokerServerProfileRequest(),
            cancellationToken);

        return profile.ToSavedBrokerServerProfile();
    }

    public async Task<SavedBrokerServerProfile> UpdateBrokerPreference(
        Guid profileId,
        SaveBrokerPreferenceRequest request,
        CancellationToken cancellationToken = default)
    {
        var existingProfile = await _brokerServerProfileService.GetServerProfileById(profileId, cancellationToken);
        if (existingProfile is null)
        {
            throw new NotFoundException($"Broker server profile '{profileId}' was not found.");
        }

        var profile = await _brokerServerProfileService.SaveServerProfile(
            request.ToSaveBrokerServerProfileRequest(existingProfile),
            cancellationToken);

        return profile.ToSavedBrokerServerProfile();
    }

    public async Task<SavedBrokerServerProfile> ActivateBrokerPreference(
        Guid profileId,
        CancellationToken cancellationToken = default)
    {
        var profile = await _brokerServerProfileService.SetActiveServerProfile(profileId, cancellationToken);
        return profile.ToSavedBrokerServerProfile();
    }
}
