using MeshBoard.Api.Middlewares.Validation;
using MeshBoard.Application.Preferences;
using MeshBoard.Contracts.Api;
using MeshBoard.Contracts.Configuration;

namespace MeshBoard.Api.Preferences;

internal static class BrokerPreferenceEndpoints
{
    private const string Tags = "BrokerPreferences";
    private const string ContentType = "application/json";

    public static IEndpointRouteBuilder MapBrokerPreferenceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(ApiRoutes.Preferences.Brokers.GetAll, GetBrokerPreferences)
            .WithName("GetBrokerPreferences")
            .Produces<IReadOnlyCollection<SavedBrokerServerProfile>>()
            .Produces(401)
            .RequireAuthorization()
            .WithTags(Tags);

        endpoints.MapPost(ApiRoutes.Preferences.Brokers.Create, CreateBrokerPreference)
            .WithName("CreateBrokerPreference")
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithValidator<SaveBrokerPreferenceRequest>()
            .Accepts<SaveBrokerPreferenceRequest>(ContentType)
            .Produces<SavedBrokerServerProfile>(201)
            .Produces(400)
            .Produces(401)
            .RequireAuthorization()
            .WithTags(Tags);

        endpoints.MapPut(ApiRoutes.Preferences.Brokers.Update, UpdateBrokerPreference)
            .WithName("UpdateBrokerPreference")
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .WithValidator<SaveBrokerPreferenceRequest>()
            .Accepts<SaveBrokerPreferenceRequest>(ContentType)
            .Produces<SavedBrokerServerProfile>()
            .Produces(400)
            .Produces(401)
            .Produces(404)
            .RequireAuthorization()
            .WithTags(Tags);

        endpoints.MapGet(ApiRoutes.Preferences.Brokers.GetActive, GetActiveBrokerPreference)
            .WithName("GetActiveBrokerPreference")
            .Produces<SavedBrokerServerProfile>()
            .Produces(401)
            .Produces(404)
            .RequireAuthorization()
            .WithTags(Tags);

        endpoints.MapPost(ApiRoutes.Preferences.Brokers.Activate, ActivateBrokerPreference)
            .WithName("ActivateBrokerPreference")
            .AddEndpointFilter<AntiforgeryValidationFilter>()
            .Produces<SavedBrokerServerProfile>()
            .Produces(400)
            .Produces(401)
            .Produces(404)
            .RequireAuthorization()
            .WithTags(Tags);

        return endpoints;
    }

    private static async Task<IResult> GetBrokerPreferences(
        IProductBrokerPreferenceService brokerPreferenceService,
        CancellationToken cancellationToken)
    {
        var profiles = await brokerPreferenceService.GetBrokerPreferences(cancellationToken);
        return Results.Ok(profiles);
    }

    private static async Task<IResult> CreateBrokerPreference(
        SaveBrokerPreferenceRequest request,
        IProductBrokerPreferenceService brokerPreferenceService,
        CancellationToken cancellationToken)
    {
        var savedProfile = await brokerPreferenceService.CreateBrokerPreference(request, cancellationToken);

        return Results.Created(
            $"{ApiRoutes.Preferences.Brokers.GetAll}/{savedProfile.Id}",
            savedProfile);
    }

    private static async Task<IResult> UpdateBrokerPreference(
        Guid id,
        SaveBrokerPreferenceRequest request,
        IProductBrokerPreferenceService brokerPreferenceService,
        CancellationToken cancellationToken)
    {
        var savedProfile = await brokerPreferenceService.UpdateBrokerPreference(id, request, cancellationToken);
        return Results.Ok(savedProfile);
    }

    private static async Task<IResult> GetActiveBrokerPreference(
        IProductBrokerPreferenceService brokerPreferenceService,
        CancellationToken cancellationToken)
    {
        var activeProfile = await brokerPreferenceService.GetActiveBrokerPreference(cancellationToken);
        return Results.Ok(activeProfile);
    }

    private static async Task<IResult> ActivateBrokerPreference(
        Guid id,
        IProductBrokerPreferenceService brokerPreferenceService,
        CancellationToken cancellationToken)
    {
        var activeProfile = await brokerPreferenceService.ActivateBrokerPreference(id, cancellationToken);
        return Results.Ok(activeProfile);
    }
}
