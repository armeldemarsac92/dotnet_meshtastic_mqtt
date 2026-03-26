using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Application.Services;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Meshtastic;
using MeshBoard.Infrastructure.Meshtastic.Hosted;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MeshBoard.UnitTests;

public sealed class MeshtasticInboundProcessingHostedServiceTests
{
    [Fact]
    public async Task StartAsync_ShouldDecodeAndIngestQueuedMessages()
    {
        var queue = new MeshtasticInboundMessageQueue(
            Options.Create(
                new MeshtasticRuntimeOptions
                {
                    InboundQueueCapacity = 8,
                    InboundWorkerCount = 1
                }),
            TimeProvider.System);
        var envelopeReader = new FakeEnvelopeReader();
        var ingestionTracker = new IngestionTracker();
        var services = new ServiceCollection();
        services.AddSingleton(ingestionTracker);
        services.AddScoped<IMeshtasticIngestionService, TrackingIngestionService>();
        await using var provider = services.BuildServiceProvider();
        var service = new MeshtasticInboundProcessingHostedService(
            queue,
            envelopeReader,
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(
                new MeshtasticRuntimeOptions
                {
                    InboundWorkerCount = 1
                }),
            TimeProvider.System,
            NullLogger<MeshtasticInboundProcessingHostedService>.Instance);

        await service.StartAsync(CancellationToken.None);

        var accepted = queue.TryEnqueue(
            new MqttInboundMessage
            {
                WorkspaceId = "workspace-a",
                BrokerServer = "mqtt-a.example.org:1883",
                Topic = "msh/US/2/e/LongFast/#",
                Payload = [0x01, 0x02],
                ReceivedAtUtc = new DateTimeOffset(2026, 3, 8, 16, 0, 0, TimeSpan.Zero)
            });

        Assert.True(accepted);

        var ingestedEnvelope = await ingestionTracker.WaitForEnvelopeAsync();
        await service.StopAsync(CancellationToken.None);

        Assert.Equal("workspace-a", envelopeReader.WorkspaceIds.Single());
        Assert.Equal("msh/US/2/e/LongFast/#", ingestedEnvelope.Topic);
        Assert.Equal("mqtt-a.example.org:1883", ingestedEnvelope.BrokerServer);
        Assert.Equal(new DateTimeOffset(2026, 3, 8, 16, 0, 0, TimeSpan.Zero), ingestedEnvelope.ReceivedAtUtc);
    }

    private sealed class FakeEnvelopeReader : IMeshtasticEnvelopeReader
    {
        public List<string> WorkspaceIds { get; } = [];

        public Task<MeshtasticEnvelope?> Read(
            string workspaceId,
            string topic,
            byte[] payload,
            CancellationToken cancellationToken = default)
        {
            WorkspaceIds.Add(workspaceId);

            return Task.FromResult<MeshtasticEnvelope?>(
                new MeshtasticEnvelope
                {
                    Topic = topic,
                    PacketType = "Text Message",
                    PayloadPreview = "decoded"
                });
        }
    }

    private sealed class IngestionTracker
    {
        private readonly TaskCompletionSource<MeshtasticEnvelope> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool HasRecordedEnvelope => _tcs.Task.IsCompletedSuccessfully;

        public Task RecordAsync(MeshtasticEnvelope envelope)
        {
            _tcs.TrySetResult(envelope);
            return Task.CompletedTask;
        }

        public Task<MeshtasticEnvelope> WaitForEnvelopeAsync()
        {
            return _tcs.Task;
        }
    }

    private sealed class TrackingIngestionService : IMeshtasticIngestionService
    {
        private readonly IngestionTracker _tracker;

        public TrackingIngestionService(IngestionTracker tracker)
        {
            _tracker = tracker;
        }

        public Task IngestEnvelope(MeshtasticEnvelope envelope, CancellationToken cancellationToken = default)
        {
            return _tracker.RecordAsync(envelope);
        }
    }
}
