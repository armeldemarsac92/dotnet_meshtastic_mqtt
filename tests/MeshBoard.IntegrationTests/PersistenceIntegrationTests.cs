using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Application.DependencyInjection;
using MeshBoard.Application.Services;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Messages;
using MeshBoard.Contracts.Meshtastic;
using MeshBoard.Contracts.Nodes;
using MeshBoard.Infrastructure.Persistence.DependencyInjection;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MeshBoard.IntegrationTests;

public sealed class PersistenceIntegrationTests
{
    [Fact]
    public async Task Initialization_ShouldBackfillLegacyRows_AndSupportTelemetryUpsert()
    {
        var databasePath = CreateTemporaryDatabasePath();

        try
        {
            await SeedLegacyDatabaseAsync(databasePath);

            await using var provider = CreateServiceProvider(databasePath, includeApplicationServices: true);
            var hostedServices = provider.GetServices<IHostedService>().ToArray();
            await StartHostedServicesAsync(hostedServices);

            try
            {
                await using var scope = provider.CreateAsyncScope();
                var messageService = scope.ServiceProvider.GetRequiredService<IMessageService>();
                var messageRepository = scope.ServiceProvider.GetRequiredService<IMessageRepository>();
                var nodeRepository = scope.ServiceProvider.GetRequiredService<INodeRepository>();
                var nodeService = scope.ServiceProvider.GetRequiredService<INodeService>();

                var messages = await messageService.GetRecentMessages(10);
                var legacyMessage = Assert.Single(messages);
                Assert.Equal("Telemetry", legacyMessage.PacketType);

                var duplicateInserted = await messageRepository.AddAsync(
                    new SaveObservedMessageRequest
                    {
                        Topic = "msh/US/2/e/legacy",
                        PacketType = "Telemetry",
                        MessageKey = "00000000-0000-0000-0000-000000000001",
                        FromNodeId = "!legacy01",
                        PayloadPreview = "should be ignored",
                        IsPrivate = false,
                        ReceivedAtUtc = new DateTimeOffset(2026, 3, 4, 2, 0, 0, TimeSpan.Zero)
                    });

                Assert.False(duplicateInserted);

                await nodeRepository.UpsertAsync(
                    new UpsertObservedNodeRequest
                    {
                        NodeId = "!legacy01",
                        LastHeardAtUtc = new DateTimeOffset(2026, 3, 4, 2, 5, 0, TimeSpan.Zero),
                        BatteryLevelPercent = 88,
                        Voltage = 4.11,
                        ChannelUtilization = 3.25,
                        AirUtilTx = 1.5,
                        UptimeSeconds = 1200,
                        TemperatureCelsius = 23.4,
                        RelativeHumidity = 45.8,
                        BarometricPressure = 1012.2
                    });

                var nodes = await nodeService.GetNodes(new NodeQuery { SearchText = "!legacy01" });
                var updatedNode = Assert.Single(nodes);
                Assert.Equal(88, updatedNode.BatteryLevelPercent);
                Assert.Equal(4.11, updatedNode.Voltage);
                Assert.Equal(3.25, updatedNode.ChannelUtilization);
                Assert.Equal(1.5, updatedNode.AirUtilTx);
                Assert.Equal(1200, updatedNode.UptimeSeconds);
                Assert.Equal(23.4, updatedNode.TemperatureCelsius);
                Assert.Equal(45.8, updatedNode.RelativeHumidity);
                Assert.Equal(1012.2, updatedNode.BarometricPressure);
            }
            finally
            {
                await StopHostedServicesAsync(hostedServices);
            }
        }
        finally
        {
            DeleteDatabaseFile(databasePath);
        }
    }

    [Fact]
    public async Task Ingestion_ShouldIgnoreDuplicatePacket_AndNotMutateNodeOnRollback()
    {
        var databasePath = CreateTemporaryDatabasePath();

        try
        {
            await using var provider = CreateServiceProvider(databasePath, includeApplicationServices: true);
            var hostedServices = provider.GetServices<IHostedService>().ToArray();
            await StartHostedServicesAsync(hostedServices);

            try
            {
                await using var scope = provider.CreateAsyncScope();
                var ingestionService = scope.ServiceProvider.GetRequiredService<IMeshtasticIngestionService>();
                var messageService = scope.ServiceProvider.GetRequiredService<IMessageService>();
                var nodeService = scope.ServiceProvider.GetRequiredService<INodeService>();

                var receivedAtUtc = new DateTimeOffset(2026, 3, 4, 16, 0, 0, TimeSpan.Zero);

                await ingestionService.IngestEnvelope(
                    new MeshtasticEnvelope
                    {
                        Topic = "msh/US/2/e/LongFast/!gateway01",
                        PacketType = "Text Message",
                        PacketId = 0xAABBCCDD,
                        PayloadPreview = "hello from first packet",
                        FromNodeId = "!abc12345",
                        IsPrivate = false,
                        ShortName = "Alpha",
                        LongName = "Alpha Node",
                        BatteryLevelPercent = 95,
                        ReceivedAtUtc = receivedAtUtc
                    });

                await ingestionService.IngestEnvelope(
                    new MeshtasticEnvelope
                    {
                        Topic = "msh/US/2/e/LongFast/!gateway02",
                        PacketType = "Text Message",
                        PacketId = 0xAABBCCDD,
                        PayloadPreview = "duplicate packet should be ignored",
                        FromNodeId = "!abc12345",
                        IsPrivate = false,
                        ShortName = "Mutated",
                        LongName = "Mutated Node",
                        BatteryLevelPercent = 5,
                        ReceivedAtUtc = receivedAtUtc.AddMinutes(1)
                    });

                var messages = await messageService.GetRecentMessages(10);
                var storedMessage = Assert.Single(messages);
                Assert.Equal("hello from first packet", storedMessage.PayloadPreview);

                var nodes = await nodeService.GetNodes(new NodeQuery { SearchText = "!abc12345" });
                var storedNode = Assert.Single(nodes);
                Assert.Equal("Alpha", storedNode.ShortName);
                Assert.Equal("Alpha Node", storedNode.LongName);
                Assert.Equal(95, storedNode.BatteryLevelPercent);
            }
            finally
            {
                await StopHostedServicesAsync(hostedServices);
            }
        }
        finally
        {
            DeleteDatabaseFile(databasePath);
        }
    }

    [Fact]
    public async Task RetentionService_ShouldDeleteMessagesOutsideRetentionWindow()
    {
        var databasePath = CreateTemporaryDatabasePath();
        var fixedUtcNow = new DateTimeOffset(2026, 3, 20, 12, 0, 0, TimeSpan.Zero);

        try
        {
            await using var provider = CreateServiceProvider(
                databasePath,
                includeApplicationServices: true,
                messageRetentionDays: 7,
                timeProvider: new FixedTimeProvider(fixedUtcNow));
            var hostedServices = provider.GetServices<IHostedService>().ToArray();
            await StartHostedServicesAsync(hostedServices);

            try
            {
                await using var scope = provider.CreateAsyncScope();
                var messageRepository = scope.ServiceProvider.GetRequiredService<IMessageRepository>();
                var messageService = scope.ServiceProvider.GetRequiredService<IMessageService>();
                var retentionService = scope.ServiceProvider.GetRequiredService<IMessageRetentionService>();

                await messageRepository.AddAsync(
                    new SaveObservedMessageRequest
                    {
                        Topic = "msh/US/2/e/retention",
                        PacketType = "Text Message",
                        MessageKey = "retention-old-msg",
                        FromNodeId = "!retention1",
                        PayloadPreview = "old packet",
                        IsPrivate = false,
                        ReceivedAtUtc = fixedUtcNow.AddDays(-8)
                    });

                await messageRepository.AddAsync(
                    new SaveObservedMessageRequest
                    {
                        Topic = "msh/US/2/e/retention",
                        PacketType = "Text Message",
                        MessageKey = "retention-fresh-msg",
                        FromNodeId = "!retention1",
                        PayloadPreview = "fresh packet",
                        IsPrivate = false,
                        ReceivedAtUtc = fixedUtcNow.AddDays(-2)
                    });

                var deletedCount = await retentionService.PruneExpiredMessages();
                Assert.Equal(1, deletedCount);

                var messages = await messageService.GetRecentMessages(10);
                var remainingMessage = Assert.Single(messages);
                Assert.Equal("fresh packet", remainingMessage.PayloadPreview);
            }
            finally
            {
                await StopHostedServicesAsync(hostedServices);
            }
        }
        finally
        {
            DeleteDatabaseFile(databasePath);
        }
    }

    private static async Task SeedLegacyDatabaseAsync(string databasePath)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE message_history (
                id TEXT NOT NULL PRIMARY KEY,
                topic TEXT NOT NULL,
                from_node_id TEXT NOT NULL,
                to_node_id TEXT NULL,
                payload_preview TEXT NOT NULL,
                is_private INTEGER NOT NULL,
                received_at_utc TEXT NOT NULL
            );

            CREATE TABLE nodes (
                node_id TEXT NOT NULL PRIMARY KEY,
                short_name TEXT NULL,
                long_name TEXT NULL,
                last_heard_at_utc TEXT NULL,
                last_text_message_at_utc TEXT NULL,
                last_known_latitude REAL NULL,
                last_known_longitude REAL NULL
            );

            INSERT INTO message_history (
                id,
                topic,
                from_node_id,
                to_node_id,
                payload_preview,
                is_private,
                received_at_utc)
            VALUES (
                '00000000-0000-0000-0000-000000000001',
                'msh/US/2/e/legacy',
                '!legacy01',
                NULL,
                'Telemetry payload from legacy table',
                0,
                '2026-03-04T01:00:00.0000000+00:00'
            );

            INSERT INTO nodes (
                node_id,
                short_name,
                long_name,
                last_heard_at_utc,
                last_text_message_at_utc,
                last_known_latitude,
                last_known_longitude)
            VALUES (
                '!legacy01',
                'LEG',
                'Legacy Node',
                '2026-03-03T00:00:00.0000000+00:00',
                NULL,
                NULL,
                NULL
            );
            """;

        await command.ExecuteNonQueryAsync();
    }

    private static ServiceProvider CreateServiceProvider(
        string databasePath,
        bool includeApplicationServices,
        int messageRetentionDays = 30,
        TimeProvider? timeProvider = null)
    {
        var settings = new Dictionary<string, string?>
        {
            [$"{PersistenceOptions.SectionName}:Provider"] = "SQLite",
            [$"{PersistenceOptions.SectionName}:ConnectionString"] = $"Data Source={databasePath}",
            [$"{PersistenceOptions.SectionName}:MessageRetentionDays"] = messageRetentionDays.ToString(),
            [$"{BrokerOptions.SectionName}:DefaultTopicPattern"] = "msh/US/2/e/#",
            [$"{BrokerOptions.SectionName}:DownlinkTopic"] = "msh/US/2/json/mqtt/"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        if (includeApplicationServices)
        {
            services.AddApplicationServices();

            if (timeProvider is not null)
            {
                services.AddSingleton(timeProvider);
            }
        }

        services.AddPersistenceInfrastructure(configuration);

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

    private static string CreateTemporaryDatabasePath()
    {
        return Path.Combine(Path.GetTempPath(), $"meshboard-integration-{Guid.NewGuid():N}.db");
    }

    private static void DeleteDatabaseFile(string databasePath)
    {
        if (File.Exists(databasePath))
        {
            File.Delete(databasePath);
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }
    }
}
