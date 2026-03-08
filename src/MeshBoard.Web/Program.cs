using MeshBoard.Application.DependencyInjection;
using MeshBoard.Infrastructure.Meshtastic.DependencyInjection;
using MeshBoard.Infrastructure.Persistence.DependencyInjection;
using MeshBoard.Web.Components;
using MeshBoard.Web.State;
using Microsoft.AspNetCore.Components.Server.Circuits;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationServices();
builder.Services.AddMeshtasticInfrastructure(builder.Configuration);
builder.Services.AddPersistenceInfrastructure(builder.Configuration);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSingleton<CircuitHandler, ActiveCircuitMetricsCircuitHandler>();
builder.Services.AddSingleton<ServerSelectionNotifier>();
builder.Services.AddSingleton<ProjectionChangeNotifier>();
builder.Services.AddHostedService<ProjectionChangeDispatchHostedService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
