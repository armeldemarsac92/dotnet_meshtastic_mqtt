using MeshBoard.Application.Services;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Exceptions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

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

        group.MapPost(
            "/",
            async Task<IResult> (
                HttpContext httpContext,
                IAntiforgery antiforgery,
                SaveBrokerPreferenceRequest request,
                IBrokerServerProfileService brokerServerProfileService,
                CancellationToken cancellationToken) =>
            {
                await antiforgery.ValidateRequestAsync(httpContext);

                try
                {
                    var savedProfile = await brokerServerProfileService.SaveServerProfile(
                        request.ToSaveBrokerServerProfileRequest(),
                        cancellationToken);

                    return Results.Created(
                        $"/api/preferences/brokers/{savedProfile.Id}",
                        savedProfile.ToSavedBrokerServerProfile());
                }
                catch (BadRequestException exception)
                {
                    return Results.BadRequest(
                        CreateProblemDetails(StatusCodes.Status400BadRequest, "Broker save failed", exception.Message));
                }
            });

        group.MapPut(
            "/{id:guid}",
            async Task<IResult> (
                HttpContext httpContext,
                IAntiforgery antiforgery,
                Guid id,
                SaveBrokerPreferenceRequest request,
                IBrokerServerProfileService brokerServerProfileService,
                CancellationToken cancellationToken) =>
            {
                await antiforgery.ValidateRequestAsync(httpContext);

                try
                {
                    var existingProfile = await brokerServerProfileService.GetServerProfileById(id, cancellationToken);
                    if (existingProfile is null)
                    {
                        return Results.NotFound(
                            CreateProblemDetails(
                                StatusCodes.Status404NotFound,
                                "Broker not found",
                                $"Broker server profile '{id}' was not found."));
                    }

                    var savedProfile = await brokerServerProfileService.SaveServerProfile(
                        request.ToSaveBrokerServerProfileRequest(existingProfile),
                        cancellationToken);

                    return Results.Ok(savedProfile.ToSavedBrokerServerProfile());
                }
                catch (BadRequestException exception)
                {
                    return Results.BadRequest(
                        CreateProblemDetails(StatusCodes.Status400BadRequest, "Broker save failed", exception.Message));
                }
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

        group.MapPost(
            "/{id:guid}/activate",
            async Task<IResult> (
                HttpContext httpContext,
                IAntiforgery antiforgery,
                Guid id,
                IBrokerServerProfileService brokerServerProfileService,
                CancellationToken cancellationToken) =>
            {
                await antiforgery.ValidateRequestAsync(httpContext);

                try
                {
                    var activeProfile = await brokerServerProfileService.SetActiveServerProfile(id, cancellationToken);
                    return Results.Ok(activeProfile.ToSavedBrokerServerProfile());
                }
                catch (NotFoundException exception)
                {
                    return Results.NotFound(
                        CreateProblemDetails(StatusCodes.Status404NotFound, "Broker not found", exception.Message));
                }
            });

        return endpoints;
    }

    private static ProblemDetails CreateProblemDetails(int statusCode, string title, string detail)
    {
        return new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        };
    }
}
