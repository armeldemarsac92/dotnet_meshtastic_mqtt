using FluentValidation;
using MeshBoard.Api.Authentication;
using MeshBoard.Api.Extensions;
using MeshBoard.Api.Middlewares.ExceptionHandlers;
using MeshBoard.Application.Abstractions.Collector;
using MeshBoard.Application.Abstractions.Authentication;
using MeshBoard.Application.Abstractions.Workspaces;
using MeshBoard.Application.DependencyInjection;
using MeshBoard.Application.Realtime;
using MeshBoard.Contracts.Authentication;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Infrastructure.Neo4j.DependencyInjection;
using MeshBoard.Infrastructure.Neo4j.Persistence;
using MeshBoard.Infrastructure.Persistence.DependencyInjection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<BadRequestExceptionHandler>();
builder.Services.AddExceptionHandler<NotFoundExceptionHandler>();
builder.Services.AddExceptionHandler<ConflictExceptionHandler>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddApiApplicationServices();
builder.Services.AddCollectorReadApplicationServices();
builder.Services.AddNeo4jInfrastructure(builder.Configuration);
builder.Services.AddScoped<ITopologyReadAdapter, Neo4jTopologyReadRepository>();
builder.Services.AddProductPersistenceInfrastructure(builder.Configuration);
builder.Services.AddCollectorReadPersistenceInfrastructure(builder.Configuration);
builder.Services.AddOptions<RealtimeSessionOptions>()
    .Bind(builder.Configuration.GetSection(RealtimeSessionOptions.SectionName));
builder.Services.AddOptions<RealtimeDownstreamBrokerOptions>()
    .Bind(builder.Configuration.GetSection(RealtimeDownstreamBrokerOptions.SectionName));
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "MeshBoard.Api.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async context =>
            {
                if (WorkspacePrincipalResolver.ResolveWorkspaceId(context.Principal) is not null)
                {
                    return;
                }

                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            },
            OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            },
            OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }
        };
});
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "MeshBoard.Api.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.HeaderName = "X-CSRF-TOKEN";
});
builder.Services.AddScoped<ICurrentUserContextAccessor, HttpContextUserContextAccessor>();
builder.Services.AddScoped<IRealtimeSessionService, RealtimeSessionService>();
builder.Services.AddScoped<IWorkspaceContextAccessor, HttpContextWorkspaceContextAccessor>();

var app = builder.Build();

var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedHeadersOptions.KnownIPNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

app.UseExceptionHandler();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapApiEndpoints();

app.Run();

public partial class Program;
