using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Application.DependencyInjection;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Meshtastic;
using MeshBoard.Infrastructure.Persistence.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace MeshBoard.IntegrationTests;

public sealed class CollectorNeighborLinkRollupIntegrationTests
{
    [Fact]
    public async Task UpsertAsync_ShouldUpdateHourlyNeighborLinkRollups()
    {
        var databaseName = $"meshboard_collector_link_rollup_tests_{Guid.NewGuid():N}";
        var connectionString = await SharedPostgresTestContainer.CreateDatabaseAsync(databaseName);

        try
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddCollectorApplicationServices();
            services.AddCollectorPersistenceInfrastructure(CreateConfiguration(connectionString));

            await using var provider = services.BuildServiceProvider();

            foreach (var hostedService in provider.GetServices<IHostedService>())
            {
                await hostedService.StartAsync(CancellationToken.None);
            }

            await using (var scope = provider.CreateAsyncScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<INeighborLinkRepository>();
                var baseSeenAtUtc = DateTimeOffset.Parse("2026-03-21T13:05:00Z");

                await repository.UpsertAsync(
                    "mqtt.world.example:1883",
                    "US/LongFast",
                    [
                        new NeighborLinkRecord
                        {
                            SourceNodeId = "!alpha",
                            TargetNodeId = "!bravo",
                            SnrDb = 5.5f,
                            LastSeenAtUtc = baseSeenAtUtc
                        }
                    ]);

                await repository.UpsertAsync(
                    "mqtt.world.example:1883",
                    "US/LongFast",
                    [
                        new NeighborLinkRecord
                        {
                            SourceNodeId = "!alpha",
                            TargetNodeId = "!bravo",
                            SnrDb = 7.0f,
                            LastSeenAtUtc = baseSeenAtUtc.AddMinutes(10)
                        }
                    ]);
            }

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            await using var command = new NpgsqlCommand(
                """
                SELECT observation_count, snr_sample_count, snr_sum_db, max_snr_db, last_snr_db
                FROM collector_neighbor_link_hourly_rollups
                WHERE source_node_id = '!alpha'
                  AND target_node_id = '!bravo';
                """,
                connection);
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(2, reader.GetInt32(0));
            Assert.Equal(2, reader.GetInt32(1));
            Assert.Equal(12.5d, reader.GetDouble(2), 3);
            Assert.Equal(7.0f, reader.GetFloat(3));
            Assert.Equal(7.0f, reader.GetFloat(4));
        }
        finally
        {
            await SharedPostgresTestContainer.DropDatabaseAsync(databaseName);
        }
    }

    private static IConfiguration CreateConfiguration(string connectionString)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [$"{PersistenceOptions.SectionName}:Provider"] = "PostgreSQL",
                    [$"{PersistenceOptions.SectionName}:ConnectionString"] = connectionString
                })
            .Build();
    }
}
