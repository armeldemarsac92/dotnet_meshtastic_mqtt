using MeshBoard.Application.DependencyInjection;
using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Messages;
using MeshBoard.Infrastructure.Persistence.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace MeshBoard.IntegrationTests;

public sealed class CollectorMessageRollupIntegrationTests
{
    [Fact]
    public async Task AddAsync_ShouldUpdateHourlyPacketRollups()
    {
        var databaseName = $"meshboard_collector_rollup_tests_{Guid.NewGuid():N}";
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
                var repository = scope.ServiceProvider.GetRequiredService<IMessageRepository>();
                var observedAtUtc = DateTimeOffset.Parse("2026-03-21T12:34:56Z");

                Assert.True(
                    await repository.AddAsync(
                        new SaveObservedMessageRequest
                        {
                            BrokerServer = "mqtt.world.example:1883",
                            Topic = "msh/US/2/e/LongFast/!alpha",
                            PacketType = "Position Update",
                            MessageKey = "msg-1",
                            FromNodeId = "!alpha",
                            PayloadPreview = "Position update",
                            IsPrivate = false,
                            ReceivedAtUtc = observedAtUtc
                        }));

                Assert.True(
                    await repository.AddAsync(
                        new SaveObservedMessageRequest
                        {
                            BrokerServer = "mqtt.world.example:1883",
                            Topic = "msh/US/2/e/LongFast/!alpha",
                            PacketType = "Position Update",
                            MessageKey = "msg-2",
                            FromNodeId = "!alpha",
                            PayloadPreview = "Position update",
                            IsPrivate = false,
                            ReceivedAtUtc = observedAtUtc.AddMinutes(10)
                        }));
            }

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            await using var channelCommand = new NpgsqlCommand(
                """
                SELECT packet_count
                FROM collector_channel_packet_hourly_rollups
                WHERE packet_type = 'Position Update';
                """,
                connection);
            var channelPacketCount = Convert.ToInt32(await channelCommand.ExecuteScalarAsync());

            await using var nodeCommand = new NpgsqlCommand(
                """
                SELECT packet_count
                FROM collector_node_packet_hourly_rollups
                WHERE node_id = '!alpha'
                  AND packet_type = 'Position Update';
                """,
                connection);
            var nodePacketCount = Convert.ToInt32(await nodeCommand.ExecuteScalarAsync());

            Assert.Equal(2, channelPacketCount);
            Assert.Equal(2, nodePacketCount);
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
