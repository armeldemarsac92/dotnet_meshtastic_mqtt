using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Application.Abstractions.Workspaces;
using MeshBoard.Application.DependencyInjection;
using MeshBoard.Application.Services;
using MeshBoard.Contracts.Authentication;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Favorites;
using MeshBoard.Contracts.Messages;
using MeshBoard.Contracts.Meshtastic;
using MeshBoard.Contracts.Nodes;
using MeshBoard.Contracts.Realtime;
using MeshBoard.Contracts.Topics;
using MeshBoard.Contracts.Workspaces;
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
                        MessageKey = "legacy-msg-1",
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
                        WorkspaceId = WorkspaceConstants.DefaultWorkspaceId,
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
                        WorkspaceId = WorkspaceConstants.DefaultWorkspaceId,
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
    public async Task Ingestion_ShouldRejectEnvelope_WhenWorkspaceIdIsMissing()
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

                var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => ingestionService.IngestEnvelope(
                        new MeshtasticEnvelope
                        {
                            Topic = "msh/US/2/e/LongFast/!abc12345",
                            PacketType = "Text Message",
                            PacketId = 0xAABBCCDD,
                            PayloadPreview = "missing workspace",
                            FromNodeId = "!abc12345",
                            ReceivedAtUtc = new DateTimeOffset(2026, 3, 4, 16, 0, 0, TimeSpan.Zero)
                        }));

                Assert.Equal("A workspace ID is required to ingest Meshtastic envelopes.", exception.Message);
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
    public async Task Initialization_ShouldNotSeedLegacyDefaultWorkspace_WhenDisabled()
    {
        var databasePath = CreateTemporaryDatabasePath();

        try
        {
            await using var provider = CreateServiceProvider(
                databasePath,
                includeApplicationServices: false,
                seedLegacyDefaultWorkspace: false);
            var hostedServices = provider.GetServices<IHostedService>().ToArray();
            await StartHostedServicesAsync(hostedServices);

            try
            {
                await using var scope = provider.CreateAsyncScope();
                var profileRepository = scope.ServiceProvider.GetRequiredService<IBrokerServerProfileRepository>();
                var topicPresetRepository = scope.ServiceProvider.GetRequiredService<ITopicPresetRepository>();

                var legacyProfiles = await profileRepository.GetAllAsync(WorkspaceConstants.DefaultWorkspaceId);
                var legacyPresets = await topicPresetRepository.GetAllAsync(
                    WorkspaceConstants.DefaultWorkspaceId,
                    "mqtt.meshtastic.org:1883");

                Assert.Empty(legacyProfiles);
                Assert.Empty(legacyPresets);
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

    [Fact]
    public async Task MessageRepository_ShouldPromoteDecodedPacket_WhenEncryptedDuplicateExists()
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
                var messageRepository = scope.ServiceProvider.GetRequiredService<IMessageRepository>();
                var messageService = scope.ServiceProvider.GetRequiredService<IMessageService>();

                var encryptedInserted = await messageRepository.AddAsync(
                    new SaveObservedMessageRequest
                    {
                        Topic = "msh/EU_868/2/e/MediumFast/!abcdef12",
                        PacketType = "Encrypted Packet",
                        MessageKey = "!abcdef12:00112233",
                        FromNodeId = "!abcdef12",
                        PayloadPreview = "Non-decoded Meshtastic payload (112 bytes)",
                        IsPrivate = false,
                        ReceivedAtUtc = new DateTimeOffset(2026, 3, 4, 20, 0, 0, TimeSpan.Zero)
                    });

                var decodedInserted = await messageRepository.AddAsync(
                    new SaveObservedMessageRequest
                    {
                        Topic = "msh/EU_868/2/json/MediumFast/!abcdef12",
                        PacketType = "Text Message",
                        MessageKey = "!abcdef12:00112233",
                        FromNodeId = "!abcdef12",
                        PayloadPreview = "Hello from MediumFast",
                        IsPrivate = false,
                        ReceivedAtUtc = new DateTimeOffset(2026, 3, 4, 20, 0, 0, TimeSpan.Zero)
                    });

                Assert.True(encryptedInserted);
                Assert.True(decodedInserted);

                var messages = await messageService.GetRecentMessages(10);
                var promoted = Assert.Single(messages);
                Assert.Equal("Text Message", promoted.PacketType);
                Assert.Equal("Hello from MediumFast", promoted.PayloadPreview);
                Assert.Equal("msh/EU_868/2/json/MediumFast/!abcdef12", promoted.Topic);
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
    public async Task MessageService_ShouldReturnRecentMessagesBySender()
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
                var messageRepository = scope.ServiceProvider.GetRequiredService<IMessageRepository>();
                var messageService = scope.ServiceProvider.GetRequiredService<IMessageService>();

                await messageRepository.AddAsync(
                    new SaveObservedMessageRequest
                    {
                        Topic = "msh/EU_868/2/e/MediumFast/!sender01",
                        PacketType = "Text Message",
                        MessageKey = "!sender01:00000001",
                        FromNodeId = "!sender01",
                        PayloadPreview = "first from sender",
                        IsPrivate = false,
                        ReceivedAtUtc = new DateTimeOffset(2026, 3, 5, 0, 0, 0, TimeSpan.Zero)
                    });

                await messageRepository.AddAsync(
                    new SaveObservedMessageRequest
                    {
                        Topic = "msh/EU_868/2/e/MediumFast/!other001",
                        PacketType = "Text Message",
                        MessageKey = "!other001:00000001",
                        FromNodeId = "!other001",
                        PayloadPreview = "from other sender",
                        IsPrivate = false,
                        ReceivedAtUtc = new DateTimeOffset(2026, 3, 5, 0, 0, 1, TimeSpan.Zero)
                    });

                await messageRepository.AddAsync(
                    new SaveObservedMessageRequest
                    {
                        Topic = "msh/EU_868/2/e/MediumFast/!sender01",
                        PacketType = "Text Message",
                        MessageKey = "!sender01:00000002",
                        FromNodeId = "!sender01",
                        PayloadPreview = "second from sender",
                        IsPrivate = false,
                        ReceivedAtUtc = new DateTimeOffset(2026, 3, 5, 0, 0, 2, TimeSpan.Zero)
                    });

                var senderMessages = await messageService.GetRecentMessagesBySender("!sender01", take: 10);

                Assert.Equal(2, senderMessages.Count);
                Assert.All(
                    senderMessages,
                    message => Assert.Equal("!sender01", message.FromNodeId));
                Assert.Equal(
                    "second from sender",
                    senderMessages.First().PayloadPreview);
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
    public async Task MessageService_ShouldReturnPagedMessagesBySender()
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
                var messageRepository = scope.ServiceProvider.GetRequiredService<IMessageRepository>();
                var messageService = scope.ServiceProvider.GetRequiredService<IMessageService>();

                await messageRepository.AddAsync(
                    new SaveObservedMessageRequest
                    {
                        Topic = "msh/EU_868/2/e/MediumFast/!sender01",
                        PacketType = "Text Message",
                        MessageKey = "!sender01:00000001",
                        FromNodeId = "!sender01",
                        PayloadPreview = "first from sender",
                        IsPrivate = false,
                        ReceivedAtUtc = new DateTimeOffset(2026, 3, 5, 0, 1, 0, TimeSpan.Zero)
                    });

                await messageRepository.AddAsync(
                    new SaveObservedMessageRequest
                    {
                        Topic = "msh/EU_868/2/e/MediumFast/!sender01",
                        PacketType = "Text Message",
                        MessageKey = "!sender01:00000002",
                        FromNodeId = "!sender01",
                        PayloadPreview = "second from sender",
                        IsPrivate = false,
                        ReceivedAtUtc = new DateTimeOffset(2026, 3, 5, 0, 1, 1, TimeSpan.Zero)
                    });

                await messageRepository.AddAsync(
                    new SaveObservedMessageRequest
                    {
                        Topic = "msh/EU_868/2/e/MediumFast/!sender01",
                        PacketType = "Text Message",
                        MessageKey = "!sender01:00000003",
                        FromNodeId = "!sender01",
                        PayloadPreview = "third from sender",
                        IsPrivate = false,
                        ReceivedAtUtc = new DateTimeOffset(2026, 3, 5, 0, 1, 2, TimeSpan.Zero)
                    });

                await messageRepository.AddAsync(
                    new SaveObservedMessageRequest
                    {
                        Topic = "msh/EU_868/2/e/MediumFast/!other001",
                        PacketType = "Text Message",
                        MessageKey = "!other001:00000001",
                        FromNodeId = "!other001",
                        PayloadPreview = "from other sender",
                        IsPrivate = false,
                        ReceivedAtUtc = new DateTimeOffset(2026, 3, 5, 0, 1, 3, TimeSpan.Zero)
                    });

                var firstPage = await messageService.GetMessagesPageBySender("!sender01", offset: 0, take: 2);
                var secondPage = await messageService.GetMessagesPageBySender("!sender01", offset: 2, take: 2);

                Assert.Equal(3, firstPage.TotalCount);
                Assert.Equal(
                    ["third from sender", "second from sender"],
                    firstPage.Items.Select(message => message.PayloadPreview).ToArray());

                Assert.Equal(3, secondPage.TotalCount);
                Assert.Single(secondPage.Items);
                Assert.Equal("first from sender", secondPage.Items.Single().PayloadPreview);
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
    public async Task MessageService_ShouldReturnRecentMessagesByChannel()
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
                var messageRepository = scope.ServiceProvider.GetRequiredService<IMessageRepository>();
                var messageService = scope.ServiceProvider.GetRequiredService<IMessageService>();

                await messageRepository.AddAsync(
                    new SaveObservedMessageRequest
                    {
                        Topic = "msh/US/2/e/LongFast/!node0001",
                        PacketType = "Text Message",
                        MessageKey = "!node0001:00000001",
                        FromNodeId = "!node0001",
                        PayloadPreview = "encrypted transport",
                        IsPrivate = false,
                        ReceivedAtUtc = new DateTimeOffset(2026, 3, 5, 1, 0, 0, TimeSpan.Zero)
                    });

                await messageRepository.AddAsync(
                    new SaveObservedMessageRequest
                    {
                        Topic = "msh/US/2/json/LongFast/!node0002",
                        PacketType = "Text Message",
                        MessageKey = "!node0002:00000001",
                        FromNodeId = "!node0002",
                        PayloadPreview = "json transport",
                        IsPrivate = false,
                        ReceivedAtUtc = new DateTimeOffset(2026, 3, 5, 1, 0, 1, TimeSpan.Zero)
                    });

                await messageRepository.AddAsync(
                    new SaveObservedMessageRequest
                    {
                        Topic = "msh/US/2/e/MediumFast/!node9999",
                        PacketType = "Text Message",
                        MessageKey = "!node9999:00000001",
                        FromNodeId = "!node9999",
                        PayloadPreview = "other channel",
                        IsPrivate = false,
                        ReceivedAtUtc = new DateTimeOffset(2026, 3, 5, 1, 0, 2, TimeSpan.Zero)
                    });

                var channelMessages = await messageService.GetRecentMessagesByChannel("US", "LongFast", take: 10);

                Assert.Equal(2, channelMessages.Count);
                Assert.All(channelMessages, message => Assert.Contains("/LongFast/", message.Topic, StringComparison.Ordinal));
                Assert.Equal(
                    ["json transport", "encrypted transport"],
                    channelMessages.Select(message => message.PayloadPreview).ToArray());
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
    public async Task ChannelReadService_ShouldReturnPagedMessagesByChannel()
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
                var channelReadService = scope.ServiceProvider.GetRequiredService<IChannelReadService>();
                var messageRepository = scope.ServiceProvider.GetRequiredService<IMessageRepository>();

                await messageRepository.AddAsync(
                    new SaveObservedMessageRequest
                    {
                        Topic = "msh/US/2/e/LongFast/!node0001",
                        PacketType = "Text Message",
                        MessageKey = "!node0001:00000001",
                        FromNodeId = "!node0001",
                        PayloadPreview = "first longfast",
                        IsPrivate = false,
                        ReceivedAtUtc = new DateTimeOffset(2026, 3, 5, 1, 5, 0, TimeSpan.Zero)
                    });

                await messageRepository.AddAsync(
                    new SaveObservedMessageRequest
                    {
                        Topic = "msh/US/2/json/LongFast/!node0002",
                        PacketType = "Text Message",
                        MessageKey = "!node0002:00000001",
                        FromNodeId = "!node0002",
                        PayloadPreview = "second longfast",
                        IsPrivate = false,
                        ReceivedAtUtc = new DateTimeOffset(2026, 3, 5, 1, 5, 1, TimeSpan.Zero)
                    });

                await messageRepository.AddAsync(
                    new SaveObservedMessageRequest
                    {
                        Topic = "msh/US/2/e/LongFast/!node0003",
                        PacketType = "Text Message",
                        MessageKey = "!node0003:00000001",
                        FromNodeId = "!node0003",
                        PayloadPreview = "third longfast",
                        IsPrivate = false,
                        ReceivedAtUtc = new DateTimeOffset(2026, 3, 5, 1, 5, 2, TimeSpan.Zero)
                    });

                await messageRepository.AddAsync(
                    new SaveObservedMessageRequest
                    {
                        Topic = "msh/US/2/e/MediumFast/!node9999",
                        PacketType = "Text Message",
                        MessageKey = "!node9999:00000001",
                        FromNodeId = "!node9999",
                        PayloadPreview = "other channel",
                        IsPrivate = false,
                        ReceivedAtUtc = new DateTimeOffset(2026, 3, 5, 1, 5, 3, TimeSpan.Zero)
                    });

                var firstPage = await channelReadService.GetMessagesPageByChannel("US", "LongFast", offset: 0, take: 2);
                var secondPage = await channelReadService.GetMessagesPageByChannel("US", "LongFast", offset: 2, take: 2);

                Assert.Equal(3, firstPage.TotalCount);
                Assert.Equal(
                    ["third longfast", "second longfast"],
                    firstPage.Items.Select(message => message.PayloadPreview).ToArray());

                Assert.Equal(3, secondPage.TotalCount);
                Assert.Single(secondPage.Items);
                Assert.Equal("first longfast", secondPage.Items.Single().PayloadPreview);
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
    public async Task MessageService_ShouldReturnPagedFilteredMessages()
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
                var messageRepository = scope.ServiceProvider.GetRequiredService<IMessageRepository>();
                var messageService = scope.ServiceProvider.GetRequiredService<IMessageService>();

                await messageRepository.AddAsync(
                    new SaveObservedMessageRequest
                    {
                        BrokerServer = "mqtt.alpha:1883",
                        Topic = "msh/US/2/e/LongFast/!alpha001",
                        PacketType = "Text Message",
                        MessageKey = "!alpha001:00000001",
                        FromNodeId = "!alpha001",
                        PayloadPreview = "alpha one",
                        IsPrivate = false,
                        ReceivedAtUtc = new DateTimeOffset(2026, 3, 5, 2, 0, 0, TimeSpan.Zero)
                    });

                await messageRepository.AddAsync(
                    new SaveObservedMessageRequest
                    {
                        BrokerServer = "mqtt.alpha:1883",
                        Topic = "msh/US/2/e/LongFast/!alpha002",
                        PacketType = "Encrypted Packet",
                        MessageKey = "!alpha002:00000001",
                        FromNodeId = "!alpha002",
                        PayloadPreview = "opaque alpha",
                        IsPrivate = false,
                        ReceivedAtUtc = new DateTimeOffset(2026, 3, 5, 2, 0, 1, TimeSpan.Zero)
                    });

                await messageRepository.AddAsync(
                    new SaveObservedMessageRequest
                    {
                        BrokerServer = "mqtt.alpha:1883",
                        Topic = "msh/US/2/e/LongFast/!alpha003",
                        PacketType = "Text Message",
                        MessageKey = "!alpha003:00000001",
                        FromNodeId = "!alpha003",
                        PayloadPreview = "alpha three",
                        IsPrivate = false,
                        ReceivedAtUtc = new DateTimeOffset(2026, 3, 5, 2, 0, 2, TimeSpan.Zero)
                    });

                await messageRepository.AddAsync(
                    new SaveObservedMessageRequest
                    {
                        BrokerServer = "mqtt.alpha:1883",
                        Topic = "msh/US/2/e/LongFast/!beta0010",
                        PacketType = "Text Message",
                        MessageKey = "!beta0010:00000001",
                        FromNodeId = "!beta0010",
                        PayloadPreview = "beta only",
                        IsPrivate = false,
                        ReceivedAtUtc = new DateTimeOffset(2026, 3, 5, 2, 0, 3, TimeSpan.Zero)
                    });

                await messageRepository.AddAsync(
                    new SaveObservedMessageRequest
                    {
                        BrokerServer = "mqtt.other:1883",
                        Topic = "msh/US/2/e/LongFast/!alpha099",
                        PacketType = "Text Message",
                        MessageKey = "!alpha099:00000001",
                        FromNodeId = "!alpha099",
                        PayloadPreview = "alpha other broker",
                        IsPrivate = false,
                        ReceivedAtUtc = new DateTimeOffset(2026, 3, 5, 2, 0, 4, TimeSpan.Zero)
                    });

                var query = new MessageQuery
                {
                    BrokerServer = "mqtt.alpha:1883",
                    SearchText = "alpha",
                    Visibility = MessageVisibilityFilter.DecodedOnly
                };

                var firstPage = await messageService.GetMessagesPage(query, offset: 0, take: 1);
                var secondPage = await messageService.GetMessagesPage(query, offset: 1, take: 1);

                Assert.Equal(2, firstPage.TotalCount);
                Assert.Single(firstPage.Items);
                Assert.Equal("alpha three", firstPage.Items.Single().PayloadPreview);

                Assert.Equal(2, secondPage.TotalCount);
                Assert.Single(secondPage.Items);
                Assert.Equal("alpha one", secondPage.Items.Single().PayloadPreview);
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
    public async Task ChannelReadService_ShouldReturnSummaryAndTopNodes()
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
                var channelReadService = scope.ServiceProvider.GetRequiredService<IChannelReadService>();
                var messageRepository = scope.ServiceProvider.GetRequiredService<IMessageRepository>();
                var nodeRepository = scope.ServiceProvider.GetRequiredService<INodeRepository>();

                await nodeRepository.UpsertAsync(
                    new UpsertObservedNodeRequest
                    {
                        NodeId = "!node0001",
                        ShortName = "Alpha",
                        LastHeardChannel = "US/LongFast"
                    });

                await nodeRepository.UpsertAsync(
                    new UpsertObservedNodeRequest
                    {
                        NodeId = "!node0002",
                        LongName = "Bravo",
                        LastHeardChannel = "US/LongFast"
                    });

                await messageRepository.AddAsync(
                    new SaveObservedMessageRequest
                    {
                        Topic = "msh/US/2/e/LongFast/!node0001",
                        BrokerServer = "mqtt-a:1883",
                        PacketType = "Encrypted Packet",
                        MessageKey = "!node0001:00000001",
                        FromNodeId = "!node0001",
                        PayloadPreview = "encrypted",
                        IsPrivate = false,
                        ReceivedAtUtc = new DateTimeOffset(2026, 3, 5, 2, 0, 0, TimeSpan.Zero)
                    });

                await messageRepository.AddAsync(
                    new SaveObservedMessageRequest
                    {
                        Topic = "msh/US/2/json/LongFast/!node0001",
                        BrokerServer = "mqtt-b:1883",
                        PacketType = "Text Message",
                        MessageKey = "!node0001:00000002",
                        FromNodeId = "!node0001",
                        PayloadPreview = "decoded",
                        IsPrivate = false,
                        ReceivedAtUtc = new DateTimeOffset(2026, 3, 5, 2, 0, 1, TimeSpan.Zero)
                    });

                await messageRepository.AddAsync(
                    new SaveObservedMessageRequest
                    {
                        Topic = "msh/US/2/e/LongFast/!node0002",
                        BrokerServer = "mqtt-a:1883",
                        PacketType = "Position Update",
                        MessageKey = "!node0002:00000001",
                        FromNodeId = "!node0002",
                        PayloadPreview = "position",
                        IsPrivate = false,
                        ReceivedAtUtc = new DateTimeOffset(2026, 3, 5, 2, 0, 2, TimeSpan.Zero)
                    });

                var summary = await channelReadService.GetChannelSummary("US", "LongFast");
                var topNodes = await channelReadService.GetTopNodesByChannel("US", "LongFast", 10);

                Assert.Equal(3, summary.PacketCount);
                Assert.Equal(2, summary.UniqueSenderCount);
                Assert.Equal(2, summary.DecodedPacketCount);
                Assert.Equal(new DateTimeOffset(2026, 3, 5, 2, 0, 2, TimeSpan.Zero), summary.LastSeenAtUtc);
                Assert.Equal(["mqtt-a:1883", "mqtt-b:1883"], summary.ObservedBrokerServers);

                Assert.Collection(
                    topNodes,
                    node =>
                    {
                        Assert.Equal("!node0001", node.NodeId);
                        Assert.Equal("Alpha", node.DisplayName);
                        Assert.Equal(2, node.PacketCount);
                    },
                    node =>
                    {
                        Assert.Equal("!node0002", node.NodeId);
                        Assert.Equal("Bravo", node.DisplayName);
                        Assert.Equal(1, node.PacketCount);
                    });
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
    public async Task NodePaging_ShouldSupportFavoritesAndChannelField()
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
                var favoriteService = scope.ServiceProvider.GetRequiredService<IFavoriteNodeService>();
                var nodeRepository = scope.ServiceProvider.GetRequiredService<INodeRepository>();
                var nodeService = scope.ServiceProvider.GetRequiredService<INodeService>();

                await nodeRepository.UpsertAsync(
                    new UpsertObservedNodeRequest
                    {
                        NodeId = "!node0001",
                        LongName = "Alpha",
                        LastHeardAtUtc = new DateTimeOffset(2026, 3, 5, 12, 0, 0, TimeSpan.Zero),
                        LastHeardChannel = "US/LongFast"
                    });
                await nodeRepository.UpsertAsync(
                    new UpsertObservedNodeRequest
                    {
                        NodeId = "!node0002",
                        LongName = "Bravo",
                        LastHeardAtUtc = new DateTimeOffset(2026, 3, 5, 12, 1, 0, TimeSpan.Zero),
                        LastHeardChannel = "EU_868/MediumFast"
                    });
                await nodeRepository.UpsertAsync(
                    new UpsertObservedNodeRequest
                    {
                        NodeId = "!node0003",
                        LongName = "Charlie",
                        LastHeardAtUtc = new DateTimeOffset(2026, 3, 5, 12, 2, 0, TimeSpan.Zero),
                        LastHeardChannel = "US/LongFast"
                    });

                await favoriteService.SaveFavoriteNode(
                    new SaveFavoriteNodeRequest
                    {
                        NodeId = "!node0002",
                        LongName = "Bravo"
                    });

                var pagedResult = await nodeService.GetNodesPage(
                    new NodeQuery
                    {
                        SortBy = NodeSortOption.NameAsc
                    },
                    offset: 1,
                    take: 1);

                Assert.Equal(3, pagedResult.TotalCount);
                var pagedNode = Assert.Single(pagedResult.Items);
                Assert.Equal("Bravo", pagedNode.LongName);
                Assert.Equal("EU_868/MediumFast", pagedNode.LastHeardChannel);

                var favoritesOnly = await nodeService.GetNodesPage(
                    new NodeQuery
                    {
                        OnlyFavorites = true
                    },
                    offset: 0,
                    take: 10);

                Assert.Equal(1, favoritesOnly.TotalCount);
                var favoriteNode = Assert.Single(favoritesOnly.Items);
                Assert.Equal("!node0002", favoriteNode.NodeId);
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
    public async Task NodeService_ShouldReturnNodeById_AndLocatedNodes()
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
                var nodeRepository = scope.ServiceProvider.GetRequiredService<INodeRepository>();
                var nodeService = scope.ServiceProvider.GetRequiredService<INodeService>();

                await nodeRepository.UpsertAsync(
                    new UpsertObservedNodeRequest
                    {
                        NodeId = "!loc00001",
                        LongName = "Locator",
                        LastHeardAtUtc = new DateTimeOffset(2026, 3, 5, 12, 10, 0, TimeSpan.Zero),
                        LastHeardChannel = "US/LongFast",
                        LastKnownLatitude = 48.8566,
                        LastKnownLongitude = 2.3522
                    });

                await nodeRepository.UpsertAsync(
                    new UpsertObservedNodeRequest
                    {
                        NodeId = "!plain001",
                        LongName = "No location",
                        LastHeardAtUtc = new DateTimeOffset(2026, 3, 5, 12, 11, 0, TimeSpan.Zero),
                        LastHeardChannel = "US/LongFast"
                    });

                var node = await nodeService.GetNodeById("!loc00001");
                var locatedNodes = await nodeService.GetLocatedNodes();
                var searchedLocatedNodes = await nodeService.GetLocatedNodes("locator");

                Assert.NotNull(node);
                Assert.Equal("Locator", node.LongName);
                var locatedNode = Assert.Single(locatedNodes);
                Assert.Equal("!loc00001", locatedNode.NodeId);
                Assert.Equal(48.8566, locatedNode.LastKnownLatitude);
                Assert.Equal(2.3522, locatedNode.LastKnownLongitude);
                var searchedLocatedNode = Assert.Single(searchedLocatedNodes);
                Assert.Equal("!loc00001", searchedLocatedNode.NodeId);
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
    public async Task TopicPreset_ShouldPersistEncryptionKeyOverride()
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
                var topicPresetService = scope.ServiceProvider.GetRequiredService<ITopicPresetService>();

                var savedPreset = await topicPresetService.SaveTopicPreset(
                    new SaveTopicPresetRequest
                    {
                        Name = "EU MediumFast",
                        TopicPattern = "msh/EU_868/2/e/MediumFast/#",
                        EncryptionKeyBase64 = "d4f1bb3a20290759f0bcffabcf4e6901",
                        IsDefault = false
                    });

                Assert.Equal("1PG7OiApB1nwvP+rz05pAQ==", savedPreset.EncryptionKeyBase64);

                var presets = await topicPresetService.GetTopicPresets();
                var reloaded = presets.Single(preset => preset.TopicPattern == "msh/EU_868/2/e/MediumFast/#");
                Assert.Equal("1PG7OiApB1nwvP+rz05pAQ==", reloaded.EncryptionKeyBase64);
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
    public async Task TopicPreset_ShouldBeScopedToActiveServer()
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
                var topicPresetService = scope.ServiceProvider.GetRequiredService<ITopicPresetService>();
                var brokerServerProfileService = scope.ServiceProvider.GetRequiredService<IBrokerServerProfileService>();

                var defaultProfile = await brokerServerProfileService.GetActiveServerProfile();
                var sharedTopicPattern = "msh/US/2/e/Shared/#";

                await topicPresetService.SaveTopicPreset(
                    new SaveTopicPresetRequest
                    {
                        Name = "Default server preset",
                        TopicPattern = sharedTopicPattern,
                        EncryptionKeyBase64 = "AQ==",
                        IsDefault = false
                    });

                await brokerServerProfileService.SaveServerProfile(
                    new SaveBrokerServerProfileRequest
                    {
                        Name = "EU profile",
                        Host = "mqtt-eu.example.org",
                        Port = 1883,
                        UseTls = false,
                        Username = string.Empty,
                        Password = string.Empty,
                        DefaultTopicPattern = "msh/EU_868/2/e/#",
                        DefaultEncryptionKeyBase64 = "AQ==",
                        DownlinkTopic = "msh/EU_868/2/json/mqtt/",
                        EnableSend = true,
                        IsActive = true
                    });

                await topicPresetService.SaveTopicPreset(
                    new SaveTopicPresetRequest
                    {
                        Name = "EU server preset",
                        TopicPattern = sharedTopicPattern,
                        EncryptionKeyBase64 = "AQ==",
                        IsDefault = false
                    });

                var euPresets = await topicPresetService.GetTopicPresets();
                Assert.Contains(euPresets, preset => preset.Name == "EU server preset");
                Assert.DoesNotContain(euPresets, preset => preset.Name == "Default server preset");

                await brokerServerProfileService.SetActiveServerProfile(defaultProfile.Id);

                var defaultPresets = await topicPresetService.GetTopicPresets();
                Assert.Contains(defaultPresets, preset => preset.Name == "Default server preset");
                Assert.DoesNotContain(defaultPresets, preset => preset.Name == "EU server preset");
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
    public async Task WorkspaceScopedServices_ShouldIsolateProfilesPresetsAndFavorites()
    {
        var databasePath = CreateTemporaryDatabasePath();

        try
        {
            await using var providerA = CreateServiceProvider(
                databasePath,
                includeApplicationServices: true,
                workspaceId: "workspace-a");
            await using var providerB = CreateServiceProvider(
                databasePath,
                includeApplicationServices: true,
                workspaceId: "workspace-b");

            var hostedServicesA = providerA.GetServices<IHostedService>().ToArray();
            var hostedServicesB = providerB.GetServices<IHostedService>().ToArray();
            await StartHostedServicesAsync(hostedServicesA);
            await StartHostedServicesAsync(hostedServicesB);

            try
            {
                await using var scopeA = providerA.CreateAsyncScope();
                var profilesA = scopeA.ServiceProvider.GetRequiredService<IBrokerServerProfileService>();
                var topicPresetsA = scopeA.ServiceProvider.GetRequiredService<ITopicPresetService>();
                var favoritesA = scopeA.ServiceProvider.GetRequiredService<IFavoriteNodeService>();

                var workspaceAProfile = await profilesA.SaveServerProfile(
                    new SaveBrokerServerProfileRequest
                    {
                        Name = "Shared broker name",
                        Host = "mqtt.shared.example.org",
                        Port = 1883,
                        UseTls = false,
                        Username = string.Empty,
                        Password = string.Empty,
                        DefaultTopicPattern = "msh/US/2/e/#",
                        DefaultEncryptionKeyBase64 = null,
                        DownlinkTopic = "msh/US/2/json/mqtt/",
                        EnableSend = true,
                        IsActive = true
                    });

                await topicPresetsA.SaveTopicPreset(
                    new SaveTopicPresetRequest
                    {
                        Name = "Workspace A preset",
                        TopicPattern = "msh/US/2/e/LongFast/#",
                        EncryptionKeyBase64 = null,
                        IsDefault = false
                    });

                await favoritesA.SaveFavoriteNode(
                    new SaveFavoriteNodeRequest
                    {
                        NodeId = "!shared0001",
                        ShortName = "WA",
                        LongName = "Workspace A Favorite"
                    });

                await using var scopeB = providerB.CreateAsyncScope();
                var profilesB = scopeB.ServiceProvider.GetRequiredService<IBrokerServerProfileService>();
                var topicPresetsB = scopeB.ServiceProvider.GetRequiredService<ITopicPresetService>();
                var favoritesB = scopeB.ServiceProvider.GetRequiredService<IFavoriteNodeService>();

                var workspaceBProfile = await profilesB.SaveServerProfile(
                    new SaveBrokerServerProfileRequest
                    {
                        Name = "Shared broker name",
                        Host = "mqtt.shared.example.org",
                        Port = 1883,
                        UseTls = false,
                        Username = string.Empty,
                        Password = string.Empty,
                        DefaultTopicPattern = "msh/US/2/e/#",
                        DefaultEncryptionKeyBase64 = TopicEncryptionKey.DefaultKeyBase64,
                        DownlinkTopic = "msh/US/2/json/mqtt/",
                        EnableSend = true,
                        IsActive = true
                    });

                await topicPresetsB.SaveTopicPreset(
                    new SaveTopicPresetRequest
                    {
                        Name = "Workspace B preset",
                        TopicPattern = "msh/US/2/e/LongFast/#",
                        EncryptionKeyBase64 = null,
                        IsDefault = false
                    });

                await favoritesB.SaveFavoriteNode(
                    new SaveFavoriteNodeRequest
                    {
                        NodeId = "!shared0001",
                        ShortName = "WB",
                        LongName = "Workspace B Favorite"
                    });

                var workspaceAProfiles = await profilesA.GetServerProfiles();
                var workspaceBProfiles = await profilesB.GetServerProfiles();
                var workspaceAPresets = await topicPresetsA.GetTopicPresets();
                var workspaceBPresets = await topicPresetsB.GetTopicPresets();
                var workspaceAFavorites = await favoritesA.GetFavoriteNodes();
                var workspaceBFavorites = await favoritesB.GetFavoriteNodes();

                Assert.Single(workspaceAProfiles);
                Assert.Single(workspaceBProfiles);
                Assert.NotEqual(workspaceAProfile.Id, workspaceBProfile.Id);
                Assert.Equal("Shared broker name", workspaceAProfiles.Single().Name);
                Assert.Equal("Shared broker name", workspaceBProfiles.Single().Name);

                var presetA = Assert.Single(workspaceAPresets);
                var presetB = Assert.Single(workspaceBPresets);
                Assert.Equal("Workspace A preset", presetA.Name);
                Assert.Equal("Workspace B preset", presetB.Name);

                var favoriteA = Assert.Single(workspaceAFavorites);
                var favoriteB = Assert.Single(workspaceBFavorites);
                Assert.Equal("WA", favoriteA.ShortName);
                Assert.Equal("WB", favoriteB.ShortName);
            }
            finally
            {
                await StopHostedServicesAsync(hostedServicesB);
                await StopHostedServicesAsync(hostedServicesA);
            }
        }
        finally
        {
            DeleteDatabaseFile(databasePath);
        }
    }

    [Fact]
    public async Task MessageAndNodeReadModels_ShouldBeScopedPerWorkspace()
    {
        var databasePath = CreateTemporaryDatabasePath();

        try
        {
            await using var providerA = CreateServiceProvider(
                databasePath,
                includeApplicationServices: true,
                workspaceId: "workspace-a");
            await using var providerB = CreateServiceProvider(
                databasePath,
                includeApplicationServices: true,
                workspaceId: "workspace-b");

            var hostedServicesA = providerA.GetServices<IHostedService>().ToArray();
            var hostedServicesB = providerB.GetServices<IHostedService>().ToArray();
            await StartHostedServicesAsync(hostedServicesA);
            await StartHostedServicesAsync(hostedServicesB);

            try
            {
                await using var scopeA = providerA.CreateAsyncScope();
                var messageRepositoryA = scopeA.ServiceProvider.GetRequiredService<IMessageRepository>();
                var nodeRepositoryA = scopeA.ServiceProvider.GetRequiredService<INodeRepository>();
                var messageServiceA = scopeA.ServiceProvider.GetRequiredService<IMessageService>();
                var nodeServiceA = scopeA.ServiceProvider.GetRequiredService<INodeService>();

                await nodeRepositoryA.UpsertAsync(
                    new UpsertObservedNodeRequest
                    {
                        NodeId = "!shared-node",
                        BrokerServer = "mqtt.shared.example.org:1883",
                        ShortName = "WA",
                        LongName = "Workspace A Node",
                        LastHeardAtUtc = new DateTimeOffset(2026, 3, 6, 10, 0, 0, TimeSpan.Zero),
                        LastHeardChannel = "EU_433/Fr_Balise"
                    });

                var insertedA = await messageRepositoryA.AddAsync(
                    new SaveObservedMessageRequest
                    {
                        MessageKey = "shared-message-key",
                        BrokerServer = "mqtt.shared.example.org:1883",
                        Topic = "msh/EU_433/2/e/Fr_Balise/!shared-node",
                        PacketType = "Text Message",
                        FromNodeId = "!shared-node",
                        PayloadPreview = "Workspace A payload",
                        IsPrivate = false,
                        ReceivedAtUtc = new DateTimeOffset(2026, 3, 6, 10, 0, 0, TimeSpan.Zero)
                    });

                await using var scopeB = providerB.CreateAsyncScope();
                var messageRepositoryB = scopeB.ServiceProvider.GetRequiredService<IMessageRepository>();
                var nodeRepositoryB = scopeB.ServiceProvider.GetRequiredService<INodeRepository>();
                var messageServiceB = scopeB.ServiceProvider.GetRequiredService<IMessageService>();
                var nodeServiceB = scopeB.ServiceProvider.GetRequiredService<INodeService>();

                await nodeRepositoryB.UpsertAsync(
                    new UpsertObservedNodeRequest
                    {
                        NodeId = "!shared-node",
                        BrokerServer = "mqtt.shared.example.org:1883",
                        ShortName = "WB",
                        LongName = "Workspace B Node",
                        LastHeardAtUtc = new DateTimeOffset(2026, 3, 6, 11, 0, 0, TimeSpan.Zero),
                        LastHeardChannel = "EU_433/Fr_Balise"
                    });

                var insertedB = await messageRepositoryB.AddAsync(
                    new SaveObservedMessageRequest
                    {
                        MessageKey = "shared-message-key",
                        BrokerServer = "mqtt.shared.example.org:1883",
                        Topic = "msh/EU_433/2/e/Fr_Balise/!shared-node",
                        PacketType = "Text Message",
                        FromNodeId = "!shared-node",
                        PayloadPreview = "Workspace B payload",
                        IsPrivate = false,
                        ReceivedAtUtc = new DateTimeOffset(2026, 3, 6, 11, 0, 0, TimeSpan.Zero)
                    });

                Assert.True(insertedA);
                Assert.True(insertedB);

                var workspaceANode = await nodeServiceA.GetNodeById("!shared-node");
                var workspaceBNode = await nodeServiceB.GetNodeById("!shared-node");
                var workspaceAMessages = await messageServiceA.GetRecentMessages(take: 10);
                var workspaceBMessages = await messageServiceB.GetRecentMessages(take: 10);

                Assert.NotNull(workspaceANode);
                Assert.NotNull(workspaceBNode);
                Assert.Equal("WA", workspaceANode!.ShortName);
                Assert.Equal("WB", workspaceBNode!.ShortName);

                var messageA = Assert.Single(workspaceAMessages);
                var messageB = Assert.Single(workspaceBMessages);
                Assert.Equal("Workspace A payload", messageA.PayloadPreview);
                Assert.Equal("Workspace B payload", messageB.PayloadPreview);
            }
            finally
            {
                await StopHostedServicesAsync(hostedServicesB);
                await StopHostedServicesAsync(hostedServicesA);
            }
        }
        finally
        {
            DeleteDatabaseFile(databasePath);
        }
    }

    [Fact]
    public async Task SubscriptionIntents_ShouldBeScopedByWorkspaceAndProfile()
    {
        var databasePath = CreateTemporaryDatabasePath();

        try
        {
            await using var providerA = CreateServiceProvider(
                databasePath,
                includeApplicationServices: true,
                workspaceId: "workspace-a");
            await using var providerB = CreateServiceProvider(
                databasePath,
                includeApplicationServices: true,
                workspaceId: "workspace-b");

            var hostedServicesA = providerA.GetServices<IHostedService>().ToArray();
            var hostedServicesB = providerB.GetServices<IHostedService>().ToArray();
            await StartHostedServicesAsync(hostedServicesA);
            await StartHostedServicesAsync(hostedServicesB);

            try
            {
                await using var scopeA = providerA.CreateAsyncScope();
                var profilesA = scopeA.ServiceProvider.GetRequiredService<IBrokerServerProfileService>();
                var profileRepositoryA = scopeA.ServiceProvider.GetRequiredService<IBrokerServerProfileRepository>();
                var intentRepositoryA = scopeA.ServiceProvider.GetRequiredService<ISubscriptionIntentRepository>();

                var workspaceAProfileOne = await profilesA.SaveServerProfile(
                    new SaveBrokerServerProfileRequest
                    {
                        Name = "Workspace A profile one",
                        Host = "mqtt-a-one.example.org",
                        Port = 1883,
                        UseTls = false,
                        Username = string.Empty,
                        Password = string.Empty,
                        DefaultTopicPattern = "msh/US/2/e/#",
                        DefaultEncryptionKeyBase64 = TopicEncryptionKey.DefaultKeyBase64,
                        DownlinkTopic = "msh/US/2/json/mqtt/",
                        EnableSend = true,
                        IsActive = true
                    });

                var workspaceAProfileTwo = await profilesA.SaveServerProfile(
                    new SaveBrokerServerProfileRequest
                    {
                        Name = "Workspace A profile two",
                        Host = "mqtt-a-two.example.org",
                        Port = 1883,
                        UseTls = false,
                        Username = string.Empty,
                        Password = string.Empty,
                        DefaultTopicPattern = "msh/EU_868/2/e/#",
                        DefaultEncryptionKeyBase64 = null,
                        DownlinkTopic = "msh/EU_868/2/json/mqtt/",
                        EnableSend = true,
                        IsActive = false
                    });

                await intentRepositoryA.AddAsync("workspace-a", workspaceAProfileOne.Id, "msh/US/2/e/LongFast/#");
                await intentRepositoryA.AddAsync("workspace-a", workspaceAProfileTwo.Id, "msh/EU_868/2/e/MediumFast/#");
                await profileRepositoryA.MarkSubscriptionIntentsInitializedAsync("workspace-a", workspaceAProfileOne.Id);

                await using var scopeB = providerB.CreateAsyncScope();
                var profilesB = scopeB.ServiceProvider.GetRequiredService<IBrokerServerProfileService>();
                var profileRepositoryB = scopeB.ServiceProvider.GetRequiredService<IBrokerServerProfileRepository>();
                var intentRepositoryB = scopeB.ServiceProvider.GetRequiredService<ISubscriptionIntentRepository>();

                var workspaceBProfile = await profilesB.SaveServerProfile(
                    new SaveBrokerServerProfileRequest
                    {
                        Name = "Workspace B profile",
                        Host = "mqtt-b.example.org",
                        Port = 1883,
                        UseTls = false,
                        Username = string.Empty,
                        Password = string.Empty,
                        DefaultTopicPattern = "msh/US/2/e/#",
                        DefaultEncryptionKeyBase64 = null,
                        DownlinkTopic = "msh/US/2/json/mqtt/",
                        EnableSend = true,
                        IsActive = true
                    });

                await intentRepositoryB.AddAsync("workspace-b", workspaceBProfile.Id, "msh/US/2/e/LongFast/#");

                var workspaceAProfileOneIntents = await intentRepositoryA.GetAllAsync("workspace-a", workspaceAProfileOne.Id);
                var workspaceAProfileTwoIntents = await intentRepositoryA.GetAllAsync("workspace-a", workspaceAProfileTwo.Id);
                var workspaceBIntents = await intentRepositoryB.GetAllAsync("workspace-b", workspaceBProfile.Id);

                Assert.Equal(["msh/US/2/e/LongFast/#"], workspaceAProfileOneIntents.Select(intent => intent.TopicFilter).ToArray());
                Assert.Equal(["msh/EU_868/2/e/MediumFast/#"], workspaceAProfileTwoIntents.Select(intent => intent.TopicFilter).ToArray());
                Assert.Equal(["msh/US/2/e/LongFast/#"], workspaceBIntents.Select(intent => intent.TopicFilter).ToArray());

                Assert.True(await profileRepositoryA.AreSubscriptionIntentsInitializedAsync("workspace-a", workspaceAProfileOne.Id));
                Assert.False(await profileRepositoryA.AreSubscriptionIntentsInitializedAsync("workspace-a", workspaceAProfileTwo.Id));
                Assert.False(await profileRepositoryB.AreSubscriptionIntentsInitializedAsync("workspace-b", workspaceBProfile.Id));
            }
            finally
            {
                await StopHostedServicesAsync(hostedServicesB);
                await StopHostedServicesAsync(hostedServicesA);
            }
        }
        finally
        {
            DeleteDatabaseFile(databasePath);
        }
    }

    [Fact]
    public async Task BrokerServerProfiles_ShouldReturnActiveProfilesAcrossWorkspaces()
    {
        var databasePath = CreateTemporaryDatabasePath();

        try
        {
            await using var providerA = CreateServiceProvider(
                databasePath,
                includeApplicationServices: true,
                workspaceId: "workspace-a");
            await using var providerB = CreateServiceProvider(
                databasePath,
                includeApplicationServices: true,
                workspaceId: "workspace-b");

            var hostedServicesA = providerA.GetServices<IHostedService>().ToArray();
            var hostedServicesB = providerB.GetServices<IHostedService>().ToArray();
            await StartHostedServicesAsync(hostedServicesA);
            await StartHostedServicesAsync(hostedServicesB);

            try
            {
                await using var scopeA = providerA.CreateAsyncScope();
                var profilesA = scopeA.ServiceProvider.GetRequiredService<IBrokerServerProfileService>();

                await profilesA.SaveServerProfile(
                    new SaveBrokerServerProfileRequest
                    {
                        Name = "Workspace A profile",
                        Host = "mqtt-a.example.org",
                        Port = 1883,
                        UseTls = false,
                        Username = string.Empty,
                        Password = string.Empty,
                        DefaultTopicPattern = "msh/US/2/e/#",
                        DefaultEncryptionKeyBase64 = null,
                        DownlinkTopic = "msh/US/2/json/mqtt/",
                        EnableSend = true,
                        IsActive = true
                    });

                await using var scopeB = providerB.CreateAsyncScope();
                var profilesB = scopeB.ServiceProvider.GetRequiredService<IBrokerServerProfileService>();
                await profilesB.SaveServerProfile(
                    new SaveBrokerServerProfileRequest
                    {
                        Name = "Workspace B profile",
                        Host = "mqtt-b.example.org",
                        Port = 1883,
                        UseTls = false,
                        Username = string.Empty,
                        Password = string.Empty,
                        DefaultTopicPattern = "msh/EU_868/2/e/#",
                        DefaultEncryptionKeyBase64 = null,
                        DownlinkTopic = "msh/EU_868/2/json/mqtt/",
                        EnableSend = true,
                        IsActive = true
                    });

                var profileRepository = scopeA.ServiceProvider.GetRequiredService<IBrokerServerProfileRepository>();
                var activeProfiles = await profileRepository.GetAllActiveAsync();
                var relevantProfiles = activeProfiles
                    .Where(profile => profile.WorkspaceId is "workspace-a" or "workspace-b")
                    .ToArray();

                Assert.Equal(2, relevantProfiles.Length);
                Assert.Contains(relevantProfiles, profile => profile.WorkspaceId == "workspace-a" && profile.Profile.Name == "Workspace A profile");
                Assert.Contains(relevantProfiles, profile => profile.WorkspaceId == "workspace-b" && profile.Profile.Name == "Workspace B profile");
                Assert.All(relevantProfiles, profile => Assert.Null(profile.Profile.DefaultEncryptionKeyBase64));
            }
            finally
            {
                await StopHostedServicesAsync(hostedServicesB);
                await StopHostedServicesAsync(hostedServicesA);
            }
        }
        finally
        {
            DeleteDatabaseFile(databasePath);
        }
    }

    [Fact]
    public async Task BrokerServerProfiles_ShouldReturnOnlyUserOwnedActiveProfilesForRuntimeBootstrap()
    {
        var databasePath = CreateTemporaryDatabasePath();

        try
        {
            await using var provider = CreateServiceProvider(
                databasePath,
                includeApplicationServices: true);

            var hostedServices = provider.GetServices<IHostedService>().ToArray();
            await StartHostedServicesAsync(hostedServices);

            try
            {
                await using var scope = provider.CreateAsyncScope();
                var profileRepository = scope.ServiceProvider.GetRequiredService<IBrokerServerProfileRepository>();
                var userAccountService = scope.ServiceProvider.GetRequiredService<IUserAccountService>();

                var alphaUser = await userAccountService.RegisterAsync(
                    new RegisterUserRequest
                    {
                        Username = "alpha.runtime",
                        Password = "secret-pass"
                    });

                var betaUser = await userAccountService.RegisterAsync(
                    new RegisterUserRequest
                    {
                        Username = "beta.runtime",
                        Password = "secret-pass"
                    });

                var allActiveProfiles = await profileRepository.GetAllActiveAsync();
                var runtimeActiveProfiles = await profileRepository.GetAllActiveUserOwnedAsync();

                Assert.Contains(allActiveProfiles, profile => profile.WorkspaceId == WorkspaceConstants.DefaultWorkspaceId);
                Assert.DoesNotContain(runtimeActiveProfiles, profile => profile.WorkspaceId == WorkspaceConstants.DefaultWorkspaceId);

                Assert.Contains(runtimeActiveProfiles, profile => profile.WorkspaceId == alphaUser.Id);
                Assert.Contains(runtimeActiveProfiles, profile => profile.WorkspaceId == betaUser.Id);
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
    public async Task RuntimeCommandRepository_ShouldPersistLeaseAndRetryCommandsAcrossServiceProviders()
    {
        var databasePath = CreateTemporaryDatabasePath();

        try
        {
            await using var providerA = CreateServiceProvider(databasePath, includeApplicationServices: false);
            await using var providerB = CreateServiceProvider(databasePath, includeApplicationServices: false);

            var hostedServicesA = providerA.GetServices<IHostedService>().ToArray();
            var hostedServicesB = providerB.GetServices<IHostedService>().ToArray();
            await StartHostedServicesAsync(hostedServicesA);
            await StartHostedServicesAsync(hostedServicesB);

            try
            {
                var repositoryA = providerA.GetRequiredService<IBrokerRuntimeCommandRepository>();
                var repositoryB = providerB.GetRequiredService<IBrokerRuntimeCommandRepository>();
                var queuedAtUtc = new DateTimeOffset(2026, 3, 8, 13, 0, 0, TimeSpan.Zero);

                await repositoryA.EnqueueAsync(
                    new BrokerRuntimeCommand
                    {
                        Id = Guid.NewGuid(),
                        WorkspaceId = "workspace-a",
                        CommandType = BrokerRuntimeCommandType.Publish,
                        Topic = "msh/US/2/json/mqtt/",
                        Payload = """{"type":"sendtext","payload":"hello"}""",
                        AttemptCount = 0,
                        CreatedAtUtc = queuedAtUtc,
                        AvailableAtUtc = queuedAtUtc
                    });

                var leasedCommands = await repositoryB.LeasePendingAsync(
                    "processor-a",
                    batchSize: 10,
                    leaseDuration: TimeSpan.FromSeconds(30));

                var leasedCommand = Assert.Single(leasedCommands);
                Assert.Equal("workspace-a", leasedCommand.WorkspaceId);
                Assert.Equal(BrokerRuntimeCommandType.Publish, leasedCommand.CommandType);
                Assert.Equal(1, leasedCommand.AttemptCount);

                await repositoryB.MarkPendingAsync(
                    leasedCommand.Id,
                    queuedAtUtc.AddSeconds(-1),
                    "temporary failure");

                var retriedCommands = await repositoryA.LeasePendingAsync(
                    "processor-b",
                    batchSize: 10,
                    leaseDuration: TimeSpan.FromSeconds(30));

                var retriedCommand = Assert.Single(retriedCommands);
                Assert.Equal(leasedCommand.Id, retriedCommand.Id);
                Assert.Equal(2, retriedCommand.AttemptCount);
                Assert.Null(retriedCommand.LastError);

                await repositoryA.MarkCompletedAsync(retriedCommand.Id);

                var afterCompletion = await repositoryB.LeasePendingAsync(
                    "processor-c",
                    batchSize: 10,
                    leaseDuration: TimeSpan.FromSeconds(30));

                Assert.Empty(afterCompletion);
            }
            finally
            {
                await StopHostedServicesAsync(hostedServicesB);
                await StopHostedServicesAsync(hostedServicesA);
            }
        }
        finally
        {
            DeleteDatabaseFile(databasePath);
        }
    }

    [Fact]
    public async Task RuntimeCommandQueryService_ShouldReturnWorkspaceScopedRecentCommandStatuses()
    {
        var databasePath = CreateTemporaryDatabasePath();

        try
        {
            await using var providerA = CreateServiceProvider(
                databasePath,
                includeApplicationServices: true,
                workspaceId: "workspace-a");
            await using var providerB = CreateServiceProvider(
                databasePath,
                includeApplicationServices: true,
                workspaceId: "workspace-b");

            var hostedServicesA = providerA.GetServices<IHostedService>().ToArray();
            var hostedServicesB = providerB.GetServices<IHostedService>().ToArray();
            await StartHostedServicesAsync(hostedServicesA);
            await StartHostedServicesAsync(hostedServicesB);

            try
            {
                Guid leasedCommandId;
                Guid failedCommandId;

                await using (var scopeA = providerA.CreateAsyncScope())
                await using (var scopeB = providerB.CreateAsyncScope())
                {
                    var repositoryA = scopeA.ServiceProvider.GetRequiredService<IBrokerRuntimeCommandRepository>();
                    var repositoryB = scopeB.ServiceProvider.GetRequiredService<IBrokerRuntimeCommandRepository>();
                    var firstQueuedAtUtc = new DateTimeOffset(2026, 3, 8, 13, 0, 0, TimeSpan.Zero);
                    var secondQueuedAtUtc = firstQueuedAtUtc.AddMinutes(5);
                    var thirdQueuedAtUtc = firstQueuedAtUtc.AddMinutes(10);

                    leasedCommandId = Guid.NewGuid();
                    failedCommandId = Guid.NewGuid();

                    await repositoryA.EnqueueAsync(
                        new BrokerRuntimeCommand
                        {
                            Id = leasedCommandId,
                            WorkspaceId = "workspace-a",
                            CommandType = BrokerRuntimeCommandType.Publish,
                            Status = BrokerRuntimeCommandStatus.Pending,
                            Topic = "msh/US/2/json/mqtt/",
                            Payload = """{"type":"sendtext","payload":"hello"}""",
                            AttemptCount = 0,
                            CreatedAtUtc = firstQueuedAtUtc,
                            AvailableAtUtc = firstQueuedAtUtc
                        });

                    await repositoryA.EnqueueAsync(
                        new BrokerRuntimeCommand
                        {
                            Id = failedCommandId,
                            WorkspaceId = "workspace-a",
                            CommandType = BrokerRuntimeCommandType.ReconcileActiveProfile,
                            Status = BrokerRuntimeCommandStatus.Pending,
                            AttemptCount = 0,
                            CreatedAtUtc = secondQueuedAtUtc,
                            AvailableAtUtc = secondQueuedAtUtc
                        });

                    await repositoryB.EnqueueAsync(
                        new BrokerRuntimeCommand
                        {
                            Id = Guid.NewGuid(),
                            WorkspaceId = "workspace-b",
                            CommandType = BrokerRuntimeCommandType.SubscribeEphemeral,
                            Status = BrokerRuntimeCommandStatus.Pending,
                            TopicFilter = "msh/EU_868/2/e/MediumFast/#",
                            AttemptCount = 0,
                            CreatedAtUtc = thirdQueuedAtUtc,
                            AvailableAtUtc = thirdQueuedAtUtc
                        });

                    var leasedCommands = await repositoryB.LeasePendingAsync(
                        "processor-a",
                        batchSize: 2,
                        leaseDuration: TimeSpan.FromSeconds(30));

                    Assert.Equal(2, leasedCommands.Count);
                    Assert.All(leasedCommands, command => Assert.Equal(BrokerRuntimeCommandStatus.Leased, command.Status));

                    await repositoryB.MarkCompletedAsync(leasedCommandId);
                    await repositoryB.MarkFailedAsync(failedCommandId, "runtime unavailable");
                }

                await using var verificationScopeA = providerA.CreateAsyncScope();
                await using var verificationScopeB = providerB.CreateAsyncScope();

                var queryServiceA = verificationScopeA.ServiceProvider.GetRequiredService<IBrokerRuntimeCommandQueryService>();
                var queryServiceB = verificationScopeB.ServiceProvider.GetRequiredService<IBrokerRuntimeCommandQueryService>();

                var workspaceACommands = await queryServiceA.GetRecentCommands(10);
                var workspaceBCommands = await queryServiceB.GetRecentCommands(10);

                Assert.Collection(
                    workspaceACommands,
                    command =>
                    {
                        Assert.Equal(failedCommandId, command.Id);
                        Assert.Equal(BrokerRuntimeCommandStatus.Failed, command.Status);
                        Assert.Equal("runtime unavailable", command.LastError);
                    },
                    command =>
                    {
                        Assert.Equal(leasedCommandId, command.Id);
                        Assert.Equal(BrokerRuntimeCommandStatus.Completed, command.Status);
                        Assert.Null(command.LastError);
                    });

                var workspaceBCommand = Assert.Single(workspaceBCommands);
                Assert.Equal("workspace-b", workspaceBCommand.WorkspaceId);
                Assert.Equal(BrokerRuntimeCommandStatus.Pending, workspaceBCommand.Status);
                Assert.Equal(BrokerRuntimeCommandType.SubscribeEphemeral, workspaceBCommand.CommandType);
            }
            finally
            {
                await StopHostedServicesAsync(hostedServicesB);
                await StopHostedServicesAsync(hostedServicesA);
            }
        }
        finally
        {
            DeleteDatabaseFile(databasePath);
        }
    }

    [Fact]
    public async Task RuntimeStatusRegistry_ShouldShareWorkspaceStatusAcrossServiceProviders()
    {
        var databasePath = CreateTemporaryDatabasePath();

        try
        {
            await using var providerA = CreateServiceProvider(
                databasePath,
                includeApplicationServices: true,
                workspaceId: "workspace-a");
            await using var providerB = CreateServiceProvider(
                databasePath,
                includeApplicationServices: true,
                workspaceId: "workspace-b");

            var hostedServicesA = providerA.GetServices<IHostedService>().ToArray();
            var hostedServicesB = providerB.GetServices<IHostedService>().ToArray();
            await StartHostedServicesAsync(hostedServicesA);
            await StartHostedServicesAsync(hostedServicesB);

            try
            {
                var runtimeRegistryA = providerA.GetRequiredService<IBrokerRuntimeRegistry>();
                var runtimeRegistryB = providerB.GetRequiredService<IBrokerRuntimeRegistry>();
                var activeProfileId = Guid.NewGuid();

                runtimeRegistryA.UpdateSnapshot(
                    "workspace-a",
                    new BrokerRuntimeSnapshot
                    {
                        ActiveServerProfileId = activeProfileId,
                        ActiveServerName = "Workspace A runtime",
                        ActiveServerAddress = "mqtt-a.example.org:1883",
                        IsConnected = true,
                        LastStatusMessage = "Connected",
                        TopicFilters = ["msh/US/2/e/LongFast/#", "msh/US/2/e/LongFast/#"]
                    });

                runtimeRegistryA.UpdateSnapshot(
                    "workspace-b",
                    new BrokerRuntimeSnapshot
                    {
                        ActiveServerName = "Workspace B runtime",
                        ActiveServerAddress = "mqtt-b.example.org:1883",
                        IsConnected = false,
                        LastStatusMessage = "Disconnected",
                        TopicFilters = ["msh/EU_868/2/e/MediumFast/#"]
                    });
                runtimeRegistryA.UpdatePipelineSnapshot(
                    "workspace-a",
                    new RuntimePipelineSnapshot
                    {
                        InboundQueueCapacity = 2048,
                        InboundWorkerCount = 2,
                        InboundQueueDepth = 5,
                        InboundOldestMessageAgeMilliseconds = 900,
                        InboundEnqueuedCount = 80,
                        InboundDequeuedCount = 75,
                        InboundDroppedCount = 3,
                        UpdatedAtUtc = new DateTimeOffset(2026, 3, 8, 18, 0, 0, TimeSpan.Zero)
                    });

                var workspaceAStatus = runtimeRegistryB.GetSnapshot("workspace-a");
                var workspaceBStatus = runtimeRegistryB.GetSnapshot("workspace-b");
                var pipelineStatusA = runtimeRegistryB.GetPipelineSnapshot("workspace-a");
                var pipelineStatusB = runtimeRegistryB.GetPipelineSnapshot("workspace-b");

                Assert.Equal(activeProfileId, workspaceAStatus.ActiveServerProfileId);
                Assert.Equal("Workspace A runtime", workspaceAStatus.ActiveServerName);
                Assert.Equal("mqtt-a.example.org:1883", workspaceAStatus.ActiveServerAddress);
                Assert.True(workspaceAStatus.IsConnected);
                Assert.Equal("Connected", workspaceAStatus.LastStatusMessage);
                Assert.Equal(["msh/US/2/e/LongFast/#"], workspaceAStatus.TopicFilters);

                Assert.Null(workspaceBStatus.ActiveServerProfileId);
                Assert.Equal("Workspace B runtime", workspaceBStatus.ActiveServerName);
                Assert.Equal("mqtt-b.example.org:1883", workspaceBStatus.ActiveServerAddress);
                Assert.False(workspaceBStatus.IsConnected);
                Assert.Equal("Disconnected", workspaceBStatus.LastStatusMessage);
                Assert.Equal(["msh/EU_868/2/e/MediumFast/#"], workspaceBStatus.TopicFilters);

                Assert.Equal(2048, pipelineStatusA.InboundQueueCapacity);
                Assert.Equal(2, pipelineStatusA.InboundWorkerCount);
                Assert.Equal(5, pipelineStatusA.InboundQueueDepth);
                Assert.Equal(900, pipelineStatusA.InboundOldestMessageAgeMilliseconds);
                Assert.Equal(80, pipelineStatusA.InboundEnqueuedCount);
                Assert.Equal(75, pipelineStatusA.InboundDequeuedCount);
                Assert.Equal(3, pipelineStatusA.InboundDroppedCount);
                Assert.Equal(0, pipelineStatusB.InboundQueueCapacity);
                Assert.Equal(0, pipelineStatusB.InboundQueueDepth);
            }
            finally
            {
                await StopHostedServicesAsync(hostedServicesB);
                await StopHostedServicesAsync(hostedServicesA);
            }
        }
        finally
        {
            DeleteDatabaseFile(databasePath);
        }
    }

    [Fact]
    public async Task UserAccountService_ShouldRegisterValidateAndProvisionWorkspace()
    {
        var databasePath = CreateTemporaryDatabasePath();

        try
        {
            await using var authProvider = CreateServiceProvider(
                databasePath,
                includeApplicationServices: true);

            var authHostedServices = authProvider.GetServices<IHostedService>().ToArray();
            await StartHostedServicesAsync(authHostedServices);

            string userId;

            try
            {
                await using var authScope = authProvider.CreateAsyncScope();
                var userAccountService = authScope.ServiceProvider.GetRequiredService<IUserAccountService>();

                var user = await userAccountService.RegisterAsync(
                    new RegisterUserRequest
                    {
                        Username = "alpha.user",
                        Password = "secret-pass"
                    });

                userId = user.Id;

                var validatedUser = await userAccountService.ValidateCredentialsAsync("ALPHA.USER", "secret-pass");

                Assert.NotNull(validatedUser);
                Assert.Equal(user.Id, validatedUser!.Id);
                Assert.Equal("alpha.user", validatedUser.Username);
            }
            finally
            {
                await StopHostedServicesAsync(authHostedServices);
            }

            await using var workspaceProvider = CreateServiceProvider(
                databasePath,
                includeApplicationServices: true,
                workspaceId: userId);

            var workspaceHostedServices = workspaceProvider.GetServices<IHostedService>().ToArray();
            await StartHostedServicesAsync(workspaceHostedServices);

            try
            {
                await using var workspaceScope = workspaceProvider.CreateAsyncScope();
                var brokerServerProfileService = workspaceScope.ServiceProvider.GetRequiredService<IBrokerServerProfileService>();
                var topicPresetService = workspaceScope.ServiceProvider.GetRequiredService<ITopicPresetService>();

                var activeProfile = await brokerServerProfileService.GetActiveServerProfile();
                var presets = await topicPresetService.GetTopicPresets();

                Assert.Equal("Default server", activeProfile.Name);
                Assert.Equal("mqtt.meshtastic.org", activeProfile.Host);
                Assert.Contains(presets, preset => preset.Name == "US Public Feed");
                Assert.Contains(presets, preset => preset.Name == "EU Public Feed");
            }
            finally
            {
                await StopHostedServicesAsync(workspaceHostedServices);
            }
        }
        finally
        {
            DeleteDatabaseFile(databasePath);
        }
    }

    [Fact]
    public async Task TopicDiscovery_ShouldPersistObservedTopicPatterns()
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
                var topicDiscoveryService = scope.ServiceProvider.GetRequiredService<ITopicDiscoveryService>();

                await ingestionService.IngestEnvelope(
                    new MeshtasticEnvelope
                    {
                        WorkspaceId = WorkspaceConstants.DefaultWorkspaceId,
                        Topic = "msh/EU_868/2/e/MediumFast/!abc12345",
                        PacketType = "Encrypted Packet",
                        PacketId = 0x00010001,
                        PayloadPreview = "Non-decoded Meshtastic payload (112 bytes)",
                        FromNodeId = "!abc12345",
                        ReceivedAtUtc = new DateTimeOffset(2026, 3, 4, 22, 0, 0, TimeSpan.Zero)
                    });

                await ingestionService.IngestEnvelope(
                    new MeshtasticEnvelope
                    {
                        WorkspaceId = WorkspaceConstants.DefaultWorkspaceId,
                        Topic = "msh/EU_868/2/json/MediumFast/!abc12345",
                        PacketType = "Text Message",
                        PacketId = 0x00010001,
                        PayloadPreview = "Decoded payload from json topic",
                        FromNodeId = "!abc12345",
                        ReceivedAtUtc = new DateTimeOffset(2026, 3, 4, 22, 0, 1, TimeSpan.Zero)
                    });

                await ingestionService.IngestEnvelope(
                    new MeshtasticEnvelope
                    {
                        WorkspaceId = WorkspaceConstants.DefaultWorkspaceId,
                        Topic = "msh/US/2/e/LongFast/!def67890",
                        PacketType = "Text Message",
                        PacketId = 0x00020002,
                        PayloadPreview = "US channel payload",
                        FromNodeId = "!def67890",
                        ReceivedAtUtc = new DateTimeOffset(2026, 3, 4, 22, 0, 2, TimeSpan.Zero)
                    });

                var discoveredTopics = await topicDiscoveryService.GetDiscoveredTopics();

                Assert.Equal(2, discoveredTopics.Count);
                Assert.Contains(discoveredTopics, topic => topic.TopicPattern == "msh/EU_868/2/e/MediumFast/#");
                Assert.Contains(discoveredTopics, topic => topic.TopicPattern == "msh/US/2/e/LongFast/#");
                Assert.All(discoveredTopics, topic => Assert.False(topic.IsRecommended));
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
    public async Task TopicDiscovery_ShouldReturnTopicsForActiveServerOnly()
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
                var topicDiscoveryService = scope.ServiceProvider.GetRequiredService<ITopicDiscoveryService>();
                var brokerServerProfileService = scope.ServiceProvider.GetRequiredService<IBrokerServerProfileService>();

                var defaultProfile = await brokerServerProfileService.GetActiveServerProfile();

                await ingestionService.IngestEnvelope(
                    new MeshtasticEnvelope
                    {
                        WorkspaceId = WorkspaceConstants.DefaultWorkspaceId,
                        BrokerServer = defaultProfile.ServerAddress,
                        Topic = "msh/US/2/e/LongFast/!abc12345",
                        PacketType = "Text Message",
                        PacketId = 0x00030001,
                        PayloadPreview = "Default server message",
                        FromNodeId = "!abc12345",
                        ReceivedAtUtc = new DateTimeOffset(2026, 3, 5, 9, 0, 0, TimeSpan.Zero)
                    });

                var euProfile = await brokerServerProfileService.SaveServerProfile(
                    new SaveBrokerServerProfileRequest
                    {
                        Name = "EU server",
                        Host = "mqtt-eu.example.org",
                        Port = 1883,
                        UseTls = false,
                        Username = string.Empty,
                        Password = string.Empty,
                        DefaultTopicPattern = "msh/EU_868/2/e/#",
                        DefaultEncryptionKeyBase64 = "AQ==",
                        DownlinkTopic = "msh/EU_868/2/json/mqtt/",
                        EnableSend = true,
                        IsActive = true
                    });

                await ingestionService.IngestEnvelope(
                    new MeshtasticEnvelope
                    {
                        WorkspaceId = WorkspaceConstants.DefaultWorkspaceId,
                        BrokerServer = euProfile.ServerAddress,
                        Topic = "msh/EU_868/2/e/MediumFast/!def67890",
                        PacketType = "Text Message",
                        PacketId = 0x00030002,
                        PayloadPreview = "EU server message",
                        FromNodeId = "!def67890",
                        ReceivedAtUtc = new DateTimeOffset(2026, 3, 5, 9, 0, 1, TimeSpan.Zero)
                    });

                var euTopics = await topicDiscoveryService.GetDiscoveredTopics();
                Assert.Contains(euTopics, topic => topic.TopicPattern == "msh/EU_868/2/e/MediumFast/#");
                Assert.DoesNotContain(euTopics, topic => topic.TopicPattern == "msh/US/2/e/LongFast/#");

                await brokerServerProfileService.SetActiveServerProfile(defaultProfile.Id);

                var defaultTopics = await topicDiscoveryService.GetDiscoveredTopics();
                Assert.Contains(defaultTopics, topic => topic.TopicPattern == "msh/US/2/e/LongFast/#");
                Assert.DoesNotContain(defaultTopics, topic => topic.TopicPattern == "msh/EU_868/2/e/MediumFast/#");
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
    public async Task TopicDiscovery_ShouldIsolateObservedTopicsPerWorkspace()
    {
        var databasePath = CreateTemporaryDatabasePath();

        try
        {
            await using var providerA = CreateServiceProvider(
                databasePath,
                includeApplicationServices: true,
                workspaceId: "workspace-a");
            await using var providerB = CreateServiceProvider(
                databasePath,
                includeApplicationServices: true,
                workspaceId: "workspace-b");

            var hostedServicesA = providerA.GetServices<IHostedService>().ToArray();
            var hostedServicesB = providerB.GetServices<IHostedService>().ToArray();
            await StartHostedServicesAsync(hostedServicesA);
            await StartHostedServicesAsync(hostedServicesB);

            try
            {
                await using var scopeA = providerA.CreateAsyncScope();
                var profilesA = scopeA.ServiceProvider.GetRequiredService<IBrokerServerProfileService>();
                var discoveryA = scopeA.ServiceProvider.GetRequiredService<ITopicDiscoveryService>();
                var ingestionA = scopeA.ServiceProvider.GetRequiredService<IMeshtasticIngestionService>();

                var profileA = await profilesA.SaveServerProfile(
                    new SaveBrokerServerProfileRequest
                    {
                        Name = "Shared broker A",
                        Host = "mqtt-shared.example.org",
                        Port = 1883,
                        UseTls = false,
                        Username = string.Empty,
                        Password = string.Empty,
                        DefaultTopicPattern = "msh/US/2/e/#",
                        DefaultEncryptionKeyBase64 = TopicEncryptionKey.DefaultKeyBase64,
                        DownlinkTopic = "msh/US/2/json/mqtt/",
                        EnableSend = true,
                        IsActive = true
                    });

                await using var scopeB = providerB.CreateAsyncScope();
                var profilesB = scopeB.ServiceProvider.GetRequiredService<IBrokerServerProfileService>();
                var discoveryB = scopeB.ServiceProvider.GetRequiredService<ITopicDiscoveryService>();

                await profilesB.SaveServerProfile(
                    new SaveBrokerServerProfileRequest
                    {
                        Name = "Shared broker B",
                        Host = "mqtt-shared.example.org",
                        Port = 1883,
                        UseTls = false,
                        Username = string.Empty,
                        Password = string.Empty,
                        DefaultTopicPattern = "msh/US/2/e/#",
                        DefaultEncryptionKeyBase64 = TopicEncryptionKey.DefaultKeyBase64,
                        DownlinkTopic = "msh/US/2/json/mqtt/",
                        EnableSend = true,
                        IsActive = true
                    });

                await ingestionA.IngestEnvelope(
                    new MeshtasticEnvelope
                    {
                        WorkspaceId = "workspace-a",
                        BrokerServer = profileA.ServerAddress,
                        Topic = "msh/EU_433/2/e/Fr_Balise/!abc12345",
                        PacketType = "Text Message",
                        PacketId = 0x00040001,
                        PayloadPreview = "Workspace A only topic",
                        FromNodeId = "!abc12345",
                        ReceivedAtUtc = new DateTimeOffset(2026, 3, 5, 10, 0, 0, TimeSpan.Zero)
                    });

                var workspaceATopics = await discoveryA.GetDiscoveredTopics();
                var workspaceBTopics = await discoveryB.GetDiscoveredTopics();

                Assert.Contains(workspaceATopics, topic => topic.TopicPattern == "msh/EU_433/2/e/Fr_Balise/#");
                Assert.DoesNotContain(workspaceBTopics, topic => topic.TopicPattern == "msh/EU_433/2/e/Fr_Balise/#");
            }
            finally
            {
                await StopHostedServicesAsync(hostedServicesB);
                await StopHostedServicesAsync(hostedServicesA);
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
                'legacy-msg-1',
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
        TimeProvider? timeProvider = null,
        string? workspaceId = null,
        bool seedLegacyDefaultWorkspace = true)
    {
        var settings = new Dictionary<string, string?>
        {
            [$"{PersistenceOptions.SectionName}:Provider"] = "SQLite",
            [$"{PersistenceOptions.SectionName}:ConnectionString"] = $"Data Source={databasePath}",
            [$"{PersistenceOptions.SectionName}:MessageRetentionDays"] = messageRetentionDays.ToString(),
            [$"{PersistenceOptions.SectionName}:SeedLegacyDefaultWorkspace"] = seedLegacyDefaultWorkspace.ToString(),
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

        if (!string.IsNullOrWhiteSpace(workspaceId))
        {
            services.AddScoped<IWorkspaceContextAccessor>(_ => new FixedWorkspaceContextAccessor(workspaceId));
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
}
