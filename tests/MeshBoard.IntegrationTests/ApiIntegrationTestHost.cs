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
    internal const string RealtimeBrokerUrl = "wss://realtime.meshboard.test/mqtt";
    internal const string RealtimeAudience = "meshboard-realtime";
    internal const string RealtimeIssuer = "https://api.meshboard.test";
    internal const string RealtimeKeyId = "meshboard-test-k1";
    internal const int RealtimeTokenLifetimeMinutes = 5;

    internal const string RealtimeSigningPrivateKeyPem = """
        -----BEGIN PRIVATE KEY-----
        MIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQDF1szRawcgdJrg
        hRNPsErT985LaTCb8h513iHy9J99Ryhyw6gKkK15X5oJnvt4S5iToDMzadddfmWf
        uacG8AMTayGvmaxeoZXYWCAWWvExUih9AOM8VZguz+iLgv1fSKDKtY8npdleyCF5
        w1g307RO4E4CopwgkqXae6uxC4FIdl3sw7goqL/4o2n/Agy9bt1BIg1WPnqGJdyA
        2DgX7GUuaYMiijV/DzW2ptQNEupgOk1Xtb1c/DOwbBBKFM+N6NbkNc8HqSVI4KEX
        Y+DE4ov/wQ29KWafQqtkpPB948KDsM5gwmTEagG8cT3+Rkcf8PYSr1onJ9OmI8ib
        5s4iBo6hAgMBAAECggEADC7Q0JGWcAN2OR7AxG6/AOQY7lBlN6k2gnwiG4/AVYrg
        /QY67iGgKSH/vpltc2g2VlruZDtfkYiT0fzxAe6cReEaQzHDqVUqgNbWbQH/KdId
        H6uDEsdTlohjkdnIaAp1Kl55WQ/vvZQ16YVjxJZTG2tZKnTgh+n38Cce+MlUVgWm
        MA4IkmwjrGt6jTEOkPSPyhxp7rkU82+3lvCOLYpZQihGba3LQP/OtQz7Zhwa5eba
        nxQYjEhhstSouViJw0b1dVOIXmROGXmrEqNI4lyVwcR/B4UmfcsbZ5afFUhNXgvy
        cY1YS0R8o8F6ZC5BSq9YIp4UhnzDLP5Yu3GknWZ4VQKBgQDzbLykI9lFEE7/p2/T
        FsHlqgIhbl57Zs8K9b+q5usM3xFhZ2UXYy6Ng+sT4Zz3cVreS+OGNUQ5EkRf2SXr
        DYTc5LOwNxis5/5FrcS8ruyxM9cdQiZ+oDtksFPm2iAzVMinfrqOAJkef0+rqVe+
        hIL5D0yi9ShZKowVGnUUhmxdLQKBgQDQDzKqHr2+ljzaEZDgcpK42BVHITOjTUkl
        pUjZLZwiaEV2DWdQdMZuIlsM1rKzlDHxX6p3c8eFcYXmU8TgjHXhp0YBUrfMnZq4
        5L0ISvXtK3Wt8C07MTauClB0tHhNZZkRPIgD4/FfC3SgvKKzfeCHNF+9tXHakVEm
        OMYSSNUnxQKBgQDqaP3HepYRgcDxQ8XVmoahqPNgSi5F2xzpyvkFlFUpEe5kw/J/
        cQ01TaGkhZBoYApHIwE5DjZiVwrs2ek/zsbxCHNY79WdO9KKOunHYROhGPC/xiHX
        smk/buV82vRDOhP353uynzTUP3jzL6HFX0nYmTkNe9Oc+fHnqJCycTgNCQKBgF1M
        M1/t4RAxtp/i+KBtQDX7T69RyCIWahKjh4M73KPhNiS15fpCIykH5uRe8ktszOh8
        Caj/Fh1UxsJ+Fe7LjaDerZmyShFLKzJ1//5T/uuXbXHOHbpJW0e4AFQVCU1LndQI
        3MVB1d7U+DuL2zm53JFEfxpG3wMv3r/Q/aD9X/gxAoGAFwosqlVw1fmMrssnumCp
        EWielvCmdFFMOMZVyelmtuUiY9kBxRub08IuaxE8tw+GT+bo+nTp61X7LXkMB96W
        8Ejit4b9dEfvGSyxEfEM+FaPYS07KgxLakb8TceUW5JDzjJlLCeHBgEwB5TwhXNG
        mC6raD6FOsb/n7LP+W0jzyk=
        -----END PRIVATE KEY-----
        """;

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
                    new KeyValuePair<string, string?>("Broker:DownlinkTopic", "msh/US/2/json/mqtt/"),
                    new KeyValuePair<string, string?>("RealtimeSession:BrokerUrl", RealtimeBrokerUrl),
                    new KeyValuePair<string, string?>("RealtimeSession:Audience", RealtimeAudience),
                    new KeyValuePair<string, string?>("RealtimeSession:Issuer", RealtimeIssuer),
                    new KeyValuePair<string, string?>("RealtimeSession:KeyId", RealtimeKeyId),
                    new KeyValuePair<string, string?>("RealtimeSession:TokenLifetimeMinutes", RealtimeTokenLifetimeMinutes.ToString()),
                    new KeyValuePair<string, string?>("RealtimeSession:SigningPrivateKeyPem", RealtimeSigningPrivateKeyPem)
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
