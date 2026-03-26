using System.Net;
using MeshBoard.Api.SDK.Abstractions;
using MeshBoard.Api.SDK.DI;
using MeshBoard.RealtimeLoadTests.Api;
using MeshBoard.RealtimeLoadTests.Configuration;
using MeshBoard.RealtimeLoadTests.Load;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

var options = builder.Configuration
    .GetSection(RealtimeLoadTestOptions.SectionName)
    .Get<RealtimeLoadTestOptions>()
    ?? new RealtimeLoadTestOptions();

options.Validate();
var cookieContainer = new CookieContainer();

builder.Services.AddSingleton<IOptions<RealtimeLoadTestOptions>>(Options.Create(options));
builder.Services.AddSingleton(options);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(cookieContainer);
builder.Services.AddSingleton<IAntiforgeryRequestTokenProvider, LoadTestAntiforgeryTokenProvider>();
builder.Services.AddSingleton<IMeshBoardApiRequestConfigurator, NoOpMeshBoardApiRequestConfigurator>();
builder.Services.AddSingleton<MeshBoardLoadTestApiClient>();
builder.Services.AddSingleton<RealtimeLoadReportWriter>();
builder.Services.AddSingleton<RealtimeLoadScenarioRunner>();
builder.Services.AddMeshBoardApiSdk(
    options.GetApiBaseUri(),
    handler =>
    {
        handler.AllowAutoRedirect = false;
        handler.CookieContainer = cookieContainer;
        handler.UseCookies = true;

        if (options.AllowInsecureTls)
        {
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }
    });

using var host = builder.Build();

var runner = host.Services.GetRequiredService<RealtimeLoadScenarioRunner>();
var reportWriter = host.Services.GetRequiredService<RealtimeLoadReportWriter>();

var summary = await runner.RunAsync();
var reportPath = await reportWriter.WriteAsync(summary);

Console.WriteLine(summary.ToConsoleText(reportPath));
