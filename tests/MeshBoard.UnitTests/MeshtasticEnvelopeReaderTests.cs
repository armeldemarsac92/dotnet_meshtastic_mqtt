using Google.Protobuf;
using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Contracts.Topics;
using MeshBoard.Infrastructure.Meshtastic.Decoding;
using MeshBoard.Infrastructure.Meshtastic.Protobuf;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;

namespace MeshBoard.UnitTests;

public sealed class MeshtasticEnvelopeReaderTests
{
    [Fact]
    public async Task Read_ShouldDecodeDeviceTelemetry_FromDirectMeshPacket()
    {
        var reader = CreateReader();
        var telemetryPayload = new Telemetry
        {
            DeviceMetrics = new DeviceMetrics
            {
                BatteryLevel = 87,
                Voltage = 4.12f,
                ChannelUtilization = 16.4f,
                AirUtilTx = 3.8f,
                UptimeSeconds = 90061
            }
        };
        var meshPacket = new MeshPacket
        {
            From = 0x12345678,
            Id = 0x00ABCDEF,
            RxTime = 1_762_112_400,
            Decoded = new Data
            {
                Portnum = PortNum.TelemetryApp,
                Payload = telemetryPayload.ToByteString()
            }
        };

        var envelope = await reader.Read("msh/US/2/e/Test/!12345678", meshPacket.ToByteArray());

        Assert.NotNull(envelope);
        Assert.Equal("Telemetry", envelope.PacketType);
        Assert.Equal("!12345678", envelope.FromNodeId);
        Assert.Equal("US/Test", envelope.LastHeardChannel);
        Assert.Equal(87, envelope.BatteryLevelPercent);
        Assert.NotNull(envelope.Voltage);
        Assert.NotNull(envelope.ChannelUtilization);
        Assert.NotNull(envelope.AirUtilTx);
        Assert.Equal(4.12d, envelope.Voltage.Value, 2);
        Assert.Equal(16.4d, envelope.ChannelUtilization.Value, 1);
        Assert.Equal(3.8d, envelope.AirUtilTx.Value, 1);
        Assert.Equal(90061L, envelope.UptimeSeconds);
        Assert.Contains("Device metrics", envelope.PayloadPreview);
    }

    [Fact]
    public async Task Read_ShouldDecodeEnvironmentTelemetry_FromDirectMeshPacket()
    {
        var reader = CreateReader();
        var telemetryPayload = new Telemetry
        {
            EnvironmentMetrics = new EnvironmentMetrics
            {
                Temperature = 21.5f,
                RelativeHumidity = 54.2f,
                BarometricPressure = 1012.7f
            }
        };
        var meshPacket = new MeshPacket
        {
            From = 0x87654321,
            Id = 0x00112233,
            Decoded = new Data
            {
                Portnum = PortNum.TelemetryApp,
                Payload = telemetryPayload.ToByteString()
            }
        };

        var envelope = await reader.Read("msh/US/2/e/Test/!87654321", meshPacket.ToByteArray());

        Assert.NotNull(envelope);
        Assert.Equal("Telemetry", envelope.PacketType);
        Assert.Equal("!87654321", envelope.FromNodeId);
        Assert.NotNull(envelope.TemperatureCelsius);
        Assert.NotNull(envelope.RelativeHumidity);
        Assert.NotNull(envelope.BarometricPressure);
        Assert.Equal(21.5d, envelope.TemperatureCelsius.Value, 1);
        Assert.Equal(54.2d, envelope.RelativeHumidity.Value, 1);
        Assert.Equal(1012.7d, envelope.BarometricPressure.Value, 1);
        Assert.Contains("Environment metrics", envelope.PayloadPreview);
    }

    [Fact]
    public async Task Read_ShouldDecodeTextMessage_FromJsonTypePayload()
    {
        var reader = CreateReader();
        var jsonPayload = """
                          {
                            "type": "text",
                            "payload": "Hello from JSON topic",
                            "fromId": "!1234abcd",
                            "toId": "!ffffffff",
                            "id": 305419896,
                            "timestamp": 1762112400
                          }
                          """;

        var envelope = await reader.Read("msh/EU_868/2/json/LongFast/!1234abcd", Encoding.UTF8.GetBytes(jsonPayload));

        Assert.NotNull(envelope);
        Assert.Equal("Text Message", envelope.PacketType);
        Assert.Equal("Hello from JSON topic", envelope.PayloadPreview);
        Assert.Equal("!1234abcd", envelope.FromNodeId);
        Assert.Null(envelope.ToNodeId);
        Assert.Equal((uint)305419896, envelope.PacketId);
        Assert.Equal("EU_868/LongFast", envelope.LastHeardChannel);
    }

    [Fact]
    public async Task Read_ShouldDecodeTextMessage_FromJsonDecodedPayload()
    {
        var reader = CreateReader();
        var base64Text = Convert.ToBase64String(Encoding.UTF8.GetBytes("Decoded text from payload"));
        var jsonPayload = $$"""
                            {
                              "decoded": {
                                "portnum": "TEXT_MESSAGE_APP",
                                "payload": "{{base64Text}}",
                                "source": 2596069104,
                                "dest": 4294967295
                              },
                              "id": 3405691582
                            }
                            """;

        var envelope = await reader.Read("msh/US/2/json/MediumFast/!9abcdef0", Encoding.UTF8.GetBytes(jsonPayload));

        Assert.NotNull(envelope);
        Assert.Equal("Text Message", envelope.PacketType);
        Assert.Equal("Decoded text from payload", envelope.PayloadPreview);
        Assert.Equal("!9abcdef0", envelope.FromNodeId);
        Assert.Null(envelope.ToNodeId);
        Assert.Equal((uint)3405691582, envelope.PacketId);
        Assert.Equal("US/MediumFast", envelope.LastHeardChannel);
    }

    [Fact]
    public async Task Read_ShouldDecryptEncryptedMeshtasticPayload_WithDefaultKey()
    {
        var reader = CreateReader();
        var meshPacket = new MeshPacket
        {
            From = 4202784164,
            To = uint.MaxValue,
            Id = 1777428186,
            Encrypted = ByteString.CopyFrom(
                Convert.FromBase64String(
                    "kiDV39nDDsi8AON+Czei6zUpy+F/7E+lyIpicxJR40KXBFmPkqFUEnobI5voQadha+s="))
        };

        var envelope = await reader.Read("msh/US/2/e/LongFast/!fa8165a4", meshPacket.ToByteArray());

        Assert.NotNull(envelope);
        Assert.Equal("Node Info", envelope.PacketType);
        Assert.Equal("Meshtastic 65a4", envelope.LongName);
        Assert.Equal("65a4", envelope.ShortName);
    }

    private static MeshtasticEnvelopeReader CreateReader()
    {
        return new MeshtasticEnvelopeReader(
            new FakeTopicEncryptionKeyResolver(),
            NullLogger<MeshtasticEnvelopeReader>.Instance);
    }

    private sealed class FakeTopicEncryptionKeyResolver : ITopicEncryptionKeyResolver
    {
        public void InvalidateCache()
        {
        }

        public Task<IReadOnlyCollection<byte[]>> ResolveCandidateKeysAsync(
            string topic,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<byte[]>>(
            [
                TopicEncryptionKey.DefaultKeyBytes
            ]);
        }
    }
}
