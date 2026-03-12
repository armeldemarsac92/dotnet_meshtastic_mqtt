using MeshBoard.Application.Services;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Exceptions;

namespace MeshBoard.Api.Preferences;

internal static class BrokerPreferenceEndpointMappings
{
    public static IEndpointRouteBuilder MapBrokerPreferenceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/preferences/brokers")
            .RequireAuthorization();

        group.MapGet(
            "/",
            async Task<IResult> (
                IBrokerServerProfileService brokerServerProfileService,
                CancellationToken cancellationToken) =>
            {
                var profiles = await brokerServerProfileService.GetServerProfiles(cancellationToken);
                return Results.Ok(profiles.Select(profile => profile.ToSavedBrokerServerProfile()).ToList());
            });

        group.MapGet(
            "/active",
            async Task<IResult> (
                IBrokerServerProfileService brokerServerProfileService,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var activeProfile = await brokerServerProfileService.GetActiveServerProfile(cancellationToken);
                    return Results.Ok(activeProfile.ToSavedBrokerServerProfile());
                }
                catch (NotFoundException exception)
                {
                    return Results.NotFound(
                        new
                        {
                            title = "Active broker not found",
                            detail = exception.Message
                        });
                }
            });

        return endpoints;
    }
}
