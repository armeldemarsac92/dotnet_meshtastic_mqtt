using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Contracts.Topics;
using MeshBoard.Infrastructure.Meshtastic.Decoding;
using Microsoft.Extensions.Logging.Abstractions;
using MQTTnet;
using MQTTnet.Protocol;
using Xunit.Abstractions;

namespace MeshBoard.UnitTests;

public sealed class MeshtasticLiveDecodeSmokeTests
{
    private readonly ITestOutputHelper _output;

    public MeshtasticLiveDecodeSmokeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact(Timeout = 120_000)]
    public async Task LiveDecodeSmoke_ShouldAttemptLiveDecoding_WhenEnabled()
    {
        if (!IsEnabled("MESHBOARD_LIVE_DECODE_SMOKE"))
        {
            return;
        }

        var host = GetEnvOrDefault("MESHBOARD_LIVE_DECODE_HOST", "mqtt.meshtastic.org");
        var port = GetIntEnvOrDefault("MESHBOARD_LIVE_DECODE_PORT", 1883);
        var username = GetEnvOrDefault("MESHBOARD_LIVE_DECODE_USERNAME", "meshdev");
        var password = GetEnvOrDefault("MESHBOARD_LIVE_DECODE_PASSWORD", "large4cats");
        var topicFilter = GetEnvOrDefault("MESHBOARD_LIVE_DECODE_TOPIC", "msh/US/2/e/#");
        var listenSeconds = GetIntEnvOrDefault("MESHBOARD_LIVE_DECODE_SECONDS", 20);
        var requireTextMessage = IsEnabled("MESHBOARD_LIVE_DECODE_REQUIRE_TEXT");

        var resolver = new StaticTopicEncryptionKeyResolver(LoadCandidateKeys());
        var reader = new MeshtasticEnvelopeReader(
            resolver,
            NullLogger<MeshtasticEnvelopeReader>.Instance);

        var observedMessages = 0;
        var mappedEnvelopes = 0;
        var decodedMessages = 0;
        var textMessages = 0;
        var encryptedMessages = 0;
        var unknownMessages = 0;

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(listenSeconds + 30));
        var clientId = $"meshboard-live-smoke-{Guid.NewGuid():N}";
        var optionsBuilder = new MqttClientOptionsBuilder()
            .WithClientId(clientId)
            .WithTcpServer(host, port)
            .WithCleanSession();

        if (!string.IsNullOrWhiteSpace(username))
        {
            optionsBuilder.WithCredentials(username, password);
        }

        if (IsEnabled("MESHBOARD_LIVE_DECODE_USE_TLS"))
        {
            optionsBuilder.WithTlsOptions(_ => _.UseTls());
        }

        var client = new MqttClientFactory().CreateMqttClient();
        client.ApplicationMessageReceivedAsync += async eventArgs =>
        {
            Interlocked.Increment(ref observedMessages);

            var envelope = await reader.Read(
                eventArgs.ApplicationMessage.Topic,
                CopyPayload(eventArgs.ApplicationMessage.Payload),
                timeout.Token);

            if (envelope is null)
            {
                return;
            }

            Interlocked.Increment(ref mappedEnvelopes);

            switch (envelope.PacketType)
            {
                case "Unknown Packet":
                    Interlocked.Increment(ref unknownMessages);
                    break;
                case "Encrypted Packet":
                    Interlocked.Increment(ref encryptedMessages);
                    break;
                case "Text Message":
                    Interlocked.Increment(ref decodedMessages);
                    Interlocked.Increment(ref textMessages);
                    break;
                default:
                    Interlocked.Increment(ref decodedMessages);
                    break;
            }
        };

        await client.ConnectAsync(optionsBuilder.Build(), timeout.Token);

        try
        {
            var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(topic =>
                {
                    topic.WithTopic(topicFilter);
                    topic.WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce);
                })
                .Build();

            await client.SubscribeAsync(subscribeOptions, timeout.Token);
            await Task.Delay(TimeSpan.FromSeconds(listenSeconds), timeout.Token);
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(cancellationToken: CancellationToken.None);
            }
        }

        _output.WriteLine(
            $"Live decode smoke stats: observed={observedMessages}, mapped={mappedEnvelopes}, decoded={decodedMessages}, text={textMessages}, encrypted={encryptedMessages}, unknown={unknownMessages}, topic={topicFilter}");

        Assert.True(observedMessages > 0, "No MQTT messages observed during live decode smoke test.");
        Assert.True(mappedEnvelopes > 0, "No Meshtastic envelopes could be parsed from observed MQTT messages.");

        if (requireTextMessage)
        {
            Assert.True(
                textMessages > 0,
                "No text messages were decoded. Add candidate keys via MESHBOARD_LIVE_DECODE_KEYS and retry.");
        }
    }

    private static IReadOnlyCollection<byte[]> LoadCandidateKeys()
    {
        var keyCandidates = new List<byte[]>
        {
            TopicEncryptionKey.DefaultKeyBytes.ToArray()
        };

        var configuredKeys = Environment.GetEnvironmentVariable("MESHBOARD_LIVE_DECODE_KEYS");

        if (string.IsNullOrWhiteSpace(configuredKeys))
        {
            return keyCandidates;
        }

        var candidateTokens = configuredKeys.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var token in candidateTokens)
        {
            if (TopicEncryptionKey.TryParse(token, out var keyBytes))
            {
                keyCandidates.Add(keyBytes);
            }
        }

        return keyCandidates;
    }

    private static bool IsEnabled(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);

        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetIntEnvOrDefault(string name, int defaultValue)
    {
        var rawValue = Environment.GetEnvironmentVariable(name);
        return int.TryParse(rawValue, out var parsed) ? parsed : defaultValue;
    }

    private static string GetEnvOrDefault(string name, string defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    private static byte[] CopyPayload(System.Buffers.ReadOnlySequence<byte> payload)
    {
        var payloadBytes = new byte[payload.Length];
        var offset = 0;

        foreach (var segment in payload)
        {
            segment.Span.CopyTo(payloadBytes.AsSpan(offset));
            offset += segment.Length;
        }

        return payloadBytes;
    }

    private sealed class StaticTopicEncryptionKeyResolver : ITopicEncryptionKeyResolver
    {
        private readonly IReadOnlyCollection<byte[]> _candidateKeys;

        public StaticTopicEncryptionKeyResolver(IReadOnlyCollection<byte[]> candidateKeys)
        {
            _candidateKeys = candidateKeys;
        }

        public void InvalidateCache()
        {
        }

        public Task<IReadOnlyCollection<byte[]>> ResolveCandidateKeysAsync(
            string topic,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_candidateKeys);
        }
    }
}
