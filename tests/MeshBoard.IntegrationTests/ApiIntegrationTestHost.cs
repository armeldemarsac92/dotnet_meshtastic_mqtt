using System.Net.Http.Json;
using MeshBoard.Api;
using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Contracts.Authentication;
using MeshBoard.Contracts.Meshtastic;
using MeshBoard.Contracts.Topics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MeshBoard.IntegrationTests;

internal sealed class ApiIntegrationTestHost : WebApplicationFactory<Program>, IAsyncDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"meshboard-api-tests-{Guid.NewGuid():N}.db");

    public HttpClient CreateApiClient()
    {
        return CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });
    }

    public async Task<string> GetAntiforgeryTokenAsync(HttpClient client, CancellationToken cancellationToken = default)
    {
        var response = await client.GetAsync("/api/auth/antiforgery", cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AntiforgeryTokenResponse>(cancellationToken)
            ?? throw new InvalidOperationException("The API returned an empty antiforgery payload.");

        return payload.RequestToken;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration(
            (_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(
                [
                    new KeyValuePair<string, string?>("Persistence:Provider", "SQLite"),
                    new KeyValuePair<string, string?>("Persistence:ConnectionString", $"Data Source={_databasePath}"),
                    new KeyValuePair<string, string?>("Persistence:SeedLegacyDefaultWorkspace", "false"),
                    new KeyValuePair<string, string?>("Persistence:MessageRetentionDays", "30"),
                    new KeyValuePair<string, string?>("Broker:DefaultTopicPattern", "msh/US/2/e/#"),
                    new KeyValuePair<string, string?>("Broker:DownlinkTopic", "msh/US/2/json/mqtt/")
                ]);
            });
        builder.ConfigureServices(
            services =>
            {
                services.AddSingleton<IBrokerRuntimeCommandService, NoOpBrokerRuntimeCommandService>();
                services.AddSingleton<IBrokerRuntimeRegistry, InMemoryBrokerRuntimeRegistry>();
                services.AddSingleton<ITopicEncryptionKeyResolver, NoOpTopicEncryptionKeyResolver>();
            });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        await Task.Yield();
        TryDeleteDatabase();
    }

    private void TryDeleteDatabase()
    {
        try
        {
            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }
        catch
        {
        }
    }

    private sealed class NoOpBrokerRuntimeCommandService : IBrokerRuntimeCommandService
    {
        public Task EnsureConnectedAsync(string workspaceId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task PublishAsync(string workspaceId, string topic, string payload, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ReconcileActiveProfileAsync(string workspaceId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ResetAndReconnectActiveProfileAsync(string workspaceId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SubscribeEphemeralAsync(string workspaceId, string topicFilter, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task UnsubscribeEphemeralAsync(string workspaceId, string topicFilter, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryBrokerRuntimeRegistry : IBrokerRuntimeRegistry
    {
        public BrokerRuntimeSnapshot GetSnapshot(string workspaceId)
        {
            return new BrokerRuntimeSnapshot();
        }

        public RuntimePipelineSnapshot GetPipelineSnapshot(string workspaceId)
        {
            return new RuntimePipelineSnapshot();
        }

        public void UpdatePipelineSnapshot(string workspaceId, RuntimePipelineSnapshot snapshot)
        {
        }

        public void UpdateSnapshot(string workspaceId, BrokerRuntimeSnapshot snapshot)
        {
        }
    }

    private sealed class NoOpTopicEncryptionKeyResolver : ITopicEncryptionKeyResolver
    {
        public void InvalidateCache()
        {
        }

        public Task<IReadOnlyCollection<byte[]>> ResolveCandidateKeysAsync(
            string workspaceId,
            string topic,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<byte[]>>
            (
                [
                    TopicEncryptionKey.DefaultKeyBytes
                ]
            );
        }
    }
}
