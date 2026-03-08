using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Meshtastic;
using MeshBoard.Infrastructure.Meshtastic.Hosted;
using Microsoft.Extensions.Options;

namespace MeshBoard.UnitTests;

public sealed class MeshtasticInboundMessageQueueTests
{
    [Fact]
    public async Task TryEnqueue_ShouldDropWhenQueueIsFull_AndTrackCounts()
    {
        var queue = new MeshtasticInboundMessageQueue(
            Options.Create(
                new MeshtasticRuntimeOptions
                {
                    InboundQueueCapacity = 1
                }),
            new FixedTimeProvider(new DateTimeOffset(2026, 3, 8, 15, 30, 0, TimeSpan.Zero)));

        var firstAccepted = queue.TryEnqueue(CreateInboundMessage("msh/US/2/e/LongFast/#"));
        var secondAccepted = queue.TryEnqueue(CreateInboundMessage("msh/US/2/e/MediumFast/#"));

        Assert.True(firstAccepted);
        Assert.False(secondAccepted);

        var beforeRead = queue.GetSnapshot("workspace-a");
        Assert.Equal(1, beforeRead.CurrentDepth);
        Assert.Equal(1, beforeRead.EnqueuedCount);
        Assert.Equal(0, beforeRead.DequeuedCount);
        Assert.Equal(1, beforeRead.DroppedCount);

        queue.Complete();

        var items = new List<MeshtasticInboundQueueItem>();
        await foreach (var item in queue.ReadAllAsync())
        {
            items.Add(item);
        }

        var afterRead = queue.GetSnapshot("workspace-a");
        Assert.Single(items);
        Assert.Equal("msh/US/2/e/LongFast/#", items[0].InboundMessage.Topic);
        Assert.Equal(0, afterRead.CurrentDepth);
        Assert.Equal(1, afterRead.DequeuedCount);
    }

    private static MqttInboundMessage CreateInboundMessage(string topic)
    {
        return new MqttInboundMessage
        {
            WorkspaceId = "workspace-a",
            BrokerServer = "mqtt.example.org:1883",
            Topic = topic,
            Payload = [],
            ReceivedAtUtc = new DateTimeOffset(2026, 3, 8, 15, 30, 0, TimeSpan.Zero)
        };
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
