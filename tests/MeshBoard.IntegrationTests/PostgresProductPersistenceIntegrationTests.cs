using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Application.Abstractions.Workspaces;
using MeshBoard.Application.DependencyInjection;
using MeshBoard.Application.Services;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Topics;
using MeshBoard.Infrastructure.Persistence.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Xunit.Abstractions;

namespace MeshBoard.IntegrationTests;

public sealed class PostgresProductPersistenceIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public PostgresProductPersistenceIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ProductPersistenceOnPostgres_ShouldIsolateBrokerProfilesAndTopicPresetsAcrossWorkspaces()
    {
        await using var harness = await PostgresProductTestHarness.StartAsync(_output);
        if (!harness.IsAvailable)
        {
            _output.WriteLine($"Skipping PostgreSQL product persistence test: {harness.UnavailableReason}");
            return;
        }

        await using var providerA = CreateServiceProvider(harness.ConnectionString, "workspace-a");
        await using var providerB = CreateServiceProvider(harness.ConnectionString, "workspace-b");

        var hostedServicesA = providerA.GetServices<IHostedService>().ToArray();
        var hostedServicesB = providerB.GetServices<IHostedService>().ToArray();
        await StartHostedServicesAsync(hostedServicesA);
        await StartHostedServicesAsync(hostedServicesB);

        try
        {
            await using var scopeA = providerA.CreateAsyncScope();
            var profilesA = scopeA.ServiceProvider.GetRequiredService<IProductBrokerPreferenceService>();
            var presetsA = scopeA.ServiceProvider.GetRequiredService<IProductTopicPresetPreferenceService>();

            var brokerA = await profilesA.CreateBrokerPreference(
                new SaveBrokerPreferenceRequest
                {
                    Name = "Workspace A broker",
                    Host = "mqtt-a.example.org",
                    Port = 1883,
                    UseTls = false,
                    Username = string.Empty,
                    Password = null,
                    ClearPassword = false,
                    DefaultTopicPattern = "msh/US/2/e/A/#",
                    DownlinkTopic = "msh/US/2/json/mqtt/a/",
                    EnableSend = true
                });

            var presetA = await presetsA.SaveTopicPresetPreference(
                new SaveTopicPresetPreferenceRequest
                {
                    Name = "Workspace A preset",
                    TopicPattern = "msh/US/2/e/A/LongFast/#",
                    IsDefault = true
                });

            await using var scopeB = providerB.CreateAsyncScope();
            var profilesB = scopeB.ServiceProvider.GetRequiredService<IProductBrokerPreferenceService>();
            var presetsB = scopeB.ServiceProvider.GetRequiredService<IProductTopicPresetPreferenceService>();

            var brokerB = await profilesB.CreateBrokerPreference(
                new SaveBrokerPreferenceRequest
                {
                    Name = "Workspace B broker",
                    Host = "mqtt-b.example.org",
                    Port = 8883,
                    UseTls = true,
                    Username = "meshboard",
                    Password = "secret",
                    ClearPassword = false,
                    DefaultTopicPattern = "msh/EU/2/e/B/#",
                    DownlinkTopic = "msh/EU/2/json/mqtt/b/",
                    EnableSend = false
                });

            var presetB = await presetsB.SaveTopicPresetPreference(
                new SaveTopicPresetPreferenceRequest
                {
                    Name = "Workspace B preset",
                    TopicPattern = "msh/EU/2/e/B/MediumFast/#",
                    IsDefault = false
                });

            var allBrokersA = await profilesA.GetBrokerPreferences();
            var allBrokersB = await profilesB.GetBrokerPreferences();
            var allPresetsA = await presetsA.GetTopicPresetPreferences();
            var allPresetsB = await presetsB.GetTopicPresetPreferences();

            var storedBrokerA = Assert.Single(allBrokersA);
            var storedBrokerB = Assert.Single(allBrokersB);
            var storedPresetA = Assert.Single(allPresetsA);
            var storedPresetB = Assert.Single(allPresetsB);

            Assert.Equal(brokerA.Id, storedBrokerA.Id);
            Assert.Equal("Workspace A broker", storedBrokerA.Name);
            Assert.Equal("mqtt-a.example.org", storedBrokerA.Host);
            Assert.DoesNotContain(allBrokersA, broker => broker.Id == brokerB.Id);

            Assert.Equal(brokerB.Id, storedBrokerB.Id);
            Assert.Equal("Workspace B broker", storedBrokerB.Name);
            Assert.Equal("mqtt-b.example.org", storedBrokerB.Host);
            Assert.DoesNotContain(allBrokersB, broker => broker.Id == brokerA.Id);

            Assert.Equal(presetA.Id, storedPresetA.Id);
            Assert.Equal("Workspace A preset", storedPresetA.Name);
            Assert.DoesNotContain(allPresetsA, preset => preset.Id == presetB.Id);

            Assert.Equal(presetB.Id, storedPresetB.Id);
            Assert.Equal("Workspace B preset", storedPresetB.Name);
            Assert.DoesNotContain(allPresetsB, preset => preset.Id == presetA.Id);
        }
        finally
        {
            await StopHostedServicesAsync(hostedServicesB);
            await StopHostedServicesAsync(hostedServicesA);
        }
    }

    [Fact]
    public async Task ProductPersistenceOnPostgres_ShouldIsolateSavedChannelFiltersAcrossWorkspaces()
    {
        await using var harness = await PostgresProductTestHarness.StartAsync(_output);
        if (!harness.IsAvailable)
        {
            _output.WriteLine($"Skipping PostgreSQL saved-channel test: {harness.UnavailableReason}");
            return;
        }

        await using var providerA = CreateServiceProvider(harness.ConnectionString, "workspace-a");
        await using var providerB = CreateServiceProvider(harness.ConnectionString, "workspace-b");

        var hostedServicesA = providerA.GetServices<IHostedService>().ToArray();
        var hostedServicesB = providerB.GetServices<IHostedService>().ToArray();
        await StartHostedServicesAsync(hostedServicesA);
        await StartHostedServicesAsync(hostedServicesB);

        try
        {
            await using var scopeA = providerA.CreateAsyncScope();
            var profilesA = scopeA.ServiceProvider.GetRequiredService<IProductBrokerPreferenceService>();
            var channelsA = scopeA.ServiceProvider.GetRequiredService<ISavedChannelPreferenceService>();

            var brokerA = await profilesA.CreateBrokerPreference(
                new SaveBrokerPreferenceRequest
                {
                    Name = "Workspace A broker",
                    Host = "mqtt-a.example.org",
                    Port = 1883,
                    UseTls = false,
                    Username = string.Empty,
                    Password = null,
                    ClearPassword = false,
                    DefaultTopicPattern = "msh/US/2/e/A/#",
                    DownlinkTopic = "msh/US/2/json/mqtt/a/",
                    EnableSend = true
                });

            await channelsA.SaveChannel(
                new SaveChannelFilterRequest
                {
                    TopicFilter = "msh/US/2/e/A/LongFast/#",
                    Label = "Workspace A feed"
                });

            await using var scopeB = providerB.CreateAsyncScope();
            var profilesB = scopeB.ServiceProvider.GetRequiredService<IProductBrokerPreferenceService>();
            var channelsB = scopeB.ServiceProvider.GetRequiredService<ISavedChannelPreferenceService>();

            var brokerB = await profilesB.CreateBrokerPreference(
                new SaveBrokerPreferenceRequest
                {
                    Name = "Workspace B broker",
                    Host = "mqtt-b.example.org",
                    Port = 1883,
                    UseTls = false,
                    Username = string.Empty,
                    Password = null,
                    ClearPassword = false,
                    DefaultTopicPattern = "msh/EU/2/e/B/#",
                    DownlinkTopic = "msh/EU/2/json/mqtt/b/",
                    EnableSend = true
                });

            await channelsB.SaveChannel(
                new SaveChannelFilterRequest
                {
                    TopicFilter = "msh/EU/2/e/B/MediumFast/#",
                    Label = "Workspace B feed"
                });

            var savedChannelsA = await channelsA.GetSavedChannels();
            var savedChannelsB = await channelsB.GetSavedChannels();

            var channelA = Assert.Single(savedChannelsA);
            var channelB = Assert.Single(savedChannelsB);

            Assert.Equal("msh/US/2/e/A/LongFast/#", channelA.TopicFilter);
            Assert.Equal("Workspace A feed", channelA.Label);

            Assert.Equal("msh/EU/2/e/B/MediumFast/#", channelB.TopicFilter);
            Assert.Equal("Workspace B feed", channelB.Label);
        }
        finally
        {
            await StopHostedServicesAsync(hostedServicesB);
            await StopHostedServicesAsync(hostedServicesA);
        }
    }

    private static ServiceProvider CreateServiceProvider(string connectionString, string workspaceId)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [$"{PersistenceOptions.SectionName}:Provider"] = "PostgreSQL",
                    [$"{PersistenceOptions.SectionName}:ConnectionString"] = connectionString,
                    [$"{PersistenceOptions.SectionName}:MessageRetentionDays"] = "30",
                    [$"{PersistenceOptions.SectionName}:SeedLegacyDefaultWorkspace"] = "false"
                })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApiApplicationServices();
        services.AddScoped<IWorkspaceContextAccessor>(_ => new FixedWorkspaceContextAccessor(workspaceId));
        services.AddProductPersistenceInfrastructure(configuration);

        return services.BuildServiceProvider();
    }

    private static async Task StartHostedServicesAsync(IEnumerable<IHostedService> hostedServices)
    {
        foreach (var hostedService in hostedServices)
        {
            await hostedService.StartAsync(CancellationToken.None);
        }
    }

    private static async Task StopHostedServicesAsync(IEnumerable<IHostedService> hostedServices)
    {
        foreach (var hostedService in hostedServices.Reverse())
        {
            await hostedService.StopAsync(CancellationToken.None);
        }
    }

    private sealed class FixedWorkspaceContextAccessor : IWorkspaceContextAccessor
    {
        private readonly string _workspaceId;

        public FixedWorkspaceContextAccessor(string workspaceId)
        {
            _workspaceId = workspaceId;
        }

        public string GetWorkspaceId()
        {
            return _workspaceId;
        }
    }

    private sealed class PostgresProductTestHarness : IAsyncDisposable
    {
        private readonly ITestOutputHelper _output;

        private PostgresProductTestHarness(ITestOutputHelper output)
        {
            _output = output;
        }

        public string ConnectionString { get; private set; } = string.Empty;

        public bool IsAvailable { get; private set; }

        public string UnavailableReason { get; private set; } = string.Empty;

        public string? ContainerName { get; private set; }

        public static async Task<PostgresProductTestHarness> StartAsync(ITestOutputHelper output)
        {
            var harness = new PostgresProductTestHarness(output);
            await harness.StartInternalAsync();
            return harness;
        }

        public async ValueTask DisposeAsync()
        {
            if (string.IsNullOrWhiteSpace(ContainerName))
            {
                return;
            }

            await RunDockerCommandAsync($"rm -f {ContainerName}", CancellationToken.None);
        }

        private async Task StartInternalAsync()
        {
            if (!CommandAvailable("docker"))
            {
                UnavailableReason = "docker CLI is not installed.";
                return;
            }

            var port = AllocatePort();
            ContainerName = $"meshboard-postgres-{Guid.NewGuid():N}";
            ConnectionString = $"Host=127.0.0.1;Port={port};Database=meshboard;Username=meshboard;Password=meshboard;Pooling=false";

            var runResult = await RunDockerCommandAsync(
                $"run -d --rm --name {ContainerName} -e POSTGRES_DB=meshboard -e POSTGRES_USER=meshboard -e POSTGRES_PASSWORD=meshboard -p {port}:5432 postgres:17-alpine",
                CancellationToken.None);

            if (runResult.ExitCode != 0)
            {
                UnavailableReason = $"docker run failed: {runResult.StandardError.Trim()}";
                _output.WriteLine(UnavailableReason);
                return;
            }

            var ready = await WaitForDatabaseAsync(ConnectionString, TimeSpan.FromSeconds(30), CancellationToken.None);
            if (!ready)
            {
                UnavailableReason = "postgres container did not become ready within the timeout window.";
                _output.WriteLine(UnavailableReason);
                return;
            }

            IsAvailable = true;
        }

        private static int AllocatePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            try
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }

        private static bool CommandAvailable(string command)
        {
            try
            {
                using var process = Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = "zsh",
                        ArgumentList = { "-lc", $"command -v {command}" },
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false
                    });

                process?.WaitForExit(5000);
                return process is not null && process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private static async Task<bool> WaitForDatabaseAsync(
            string connectionString,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            var deadline = DateTimeOffset.UtcNow.Add(timeout);

            while (DateTimeOffset.UtcNow < deadline)
            {
                try
                {
                    await using var connection = new NpgsqlConnection(connectionString);
                    await connection.OpenAsync(cancellationToken);
                    return true;
                }
                catch
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
                }
            }

            return false;
        }

        private static async Task<DockerCommandResult> RunDockerCommandAsync(string arguments, CancellationToken cancellationToken)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };

            process.Start();
            var standardOutput = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var standardError = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            return new DockerCommandResult(process.ExitCode, standardOutput, standardError);
        }

        private sealed record DockerCommandResult(int ExitCode, string StandardOutput, string StandardError);
    }
}
