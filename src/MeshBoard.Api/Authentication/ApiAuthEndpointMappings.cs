using System.Security.Claims;
using MeshBoard.Application.Services;
using MeshBoard.Contracts.Api;
using MeshBoard.Contracts.Authentication;
using MeshBoard.Contracts.Exceptions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace MeshBoard.Api.Authentication;

internal static class ApiAuthEndpointMappings
{
    public static IEndpointRouteBuilder MapApiAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(ApiRoutes.Auth.Group);

        group.MapGet(
                ApiRoutes.Auth.Antiforgery,
                (HttpContext httpContext, IAntiforgery antiforgery) =>
                {
                    var tokens = antiforgery.GetAndStoreTokens(httpContext);

                    return Results.Ok(
                        new AntiforgeryTokenResponse
                        {
                            RequestToken = tokens.RequestToken ?? string.Empty
                        });
                })
            .AllowAnonymous();

        group.MapPost(
                ApiRoutes.Auth.Register,
                async Task<IResult> (
                    HttpContext httpContext,
                    IAntiforgery antiforgery,
                    RegisterUserRequest request,
                    IUserAccountService userAccountService,
                    CancellationToken cancellationToken) =>
                {
                    await antiforgery.ValidateRequestAsync(httpContext);

                    try
                    {
                        var user = await userAccountService.RegisterAsync(request, cancellationToken);
                        await SignInAsync(httpContext, user);
                        return Results.Ok(user.ToAuthenticatedUserResponse(user.Id));
                    }
                    catch (BadRequestException exception)
                    {
                        return Results.BadRequest(CreateProblemDetails(StatusCodes.Status400BadRequest, "Registration failed", exception.Message));
                    }
                    catch (ConflictException exception)
                    {
                        return Results.Conflict(CreateProblemDetails(StatusCodes.Status409Conflict, "Registration failed", exception.Message));
                    }
                })
            .AllowAnonymous();

        group.MapPost(
                ApiRoutes.Auth.Login,
                async Task<IResult> (
                    HttpContext httpContext,
                    IAntiforgery antiforgery,
                    LoginUserRequest request,
                    IUserAccountService userAccountService,
                    CancellationToken cancellationToken) =>
                {
                    await antiforgery.ValidateRequestAsync(httpContext);

                    var user = await userAccountService.ValidateCredentialsAsync(
                        request.Username,
                        request.Password,
                        cancellationToken);

                    if (user is null)
                    {
                        return Results.Unauthorized();
                    }

                    await SignInAsync(httpContext, user);
                    return Results.Ok(user.ToAuthenticatedUserResponse(user.Id));
                })
            .AllowAnonymous();

        group.MapPost(
                ApiRoutes.Auth.Logout,
                async Task<IResult> (
                    HttpContext httpContext,
                    IAntiforgery antiforgery) =>
                {
                    await antiforgery.ValidateRequestAsync(httpContext);
                    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    return Results.NoContent();
                })
            .RequireAuthorization();

        group.MapGet(
                ApiRoutes.Auth.Me,
                async Task<IResult> (
                    HttpContext httpContext,
                    IUserAccountService userAccountService,
                    CancellationToken cancellationToken) =>
                {
                    var principal = httpContext.User;
                    var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
                    var workspaceId = WorkspacePrincipalResolver.ResolveWorkspaceId(principal);

                    if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(workspaceId))
                    {
                        return Results.Unauthorized();
                    }

                    var user = await userAccountService.GetByIdAsync(userId, cancellationToken);
                    if (user is null)
                    {
                        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                        return Results.Unauthorized();
                    }

                    return Results.Ok(user.ToAuthenticatedUserResponse(workspaceId));
                })
            .RequireAuthorization();

        return endpoints;
    }

    private static AuthenticationProperties CreateAuthenticationProperties()
    {
        return new AuthenticationProperties
        {
            AllowRefresh = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14),
            IsPersistent = true
        };
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

    private static Task SignInAsync(HttpContext httpContext, AppUser user)
    {
        return httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            AppUserPrincipalFactory.CreatePrincipal(user, CookieAuthenticationDefaults.AuthenticationScheme),
            CreateAuthenticationProperties());
    }
}
