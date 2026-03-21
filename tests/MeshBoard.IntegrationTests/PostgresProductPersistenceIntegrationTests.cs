using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using MeshBoard.Application.Authentication;
using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Application.Abstractions.Workspaces;
using MeshBoard.Application.DependencyInjection;
using MeshBoard.Application.Services;
using MeshBoard.Contracts.Configuration;
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

    private static async Task EnsureWorkspaceUserAsync(ServiceProvider provider, string workspaceId)
    {
        await using var scope = provider.CreateAsyncScope();
        var userAccountRepository = scope.ServiceProvider.GetRequiredService<IUserAccountRepository>();

        if (await userAccountRepository.GetByIdAsync(workspaceId) is not null)
        {
            return;
        }

        await userAccountRepository.InsertAsync(
            new CreateUserAccountRequest
            {
                Id = workspaceId,
                Username = workspaceId,
                NormalizedUsername = workspaceId.ToUpperInvariant(),
                PasswordHash = "integration-test-hash",
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
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
