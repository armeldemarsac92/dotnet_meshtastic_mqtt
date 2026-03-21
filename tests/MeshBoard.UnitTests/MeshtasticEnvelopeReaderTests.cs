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

        var envelope = await reader.Read("workspace-tests", "msh/US/2/e/Test/!12345678", meshPacket.ToByteArray());

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

        var envelope = await reader.Read("workspace-tests", "msh/US/2/e/Test/!87654321", meshPacket.ToByteArray());

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
    public async Task Read_ShouldDecodeNeighborInfo_FromDirectMeshPacket()
    {
        var reader = CreateReader();
        var neighborPayload = new NeighborInfo
        {
            NodeId = 0x12345678,
            LastSentById = 0x87654321,
            NodeBroadcastIntervalSecs = 480
        };
        neighborPayload.Neighbors.Add(
            new Neighbor
            {
                NodeId = 0x87654321,
                Snr = 6.0f,
                LastRxTime = 1_762_112_400
            });
        neighborPayload.Neighbors.Add(
            new Neighbor
            {
                NodeId = 0xAABBCCDD,
                Snr = -11.25f
            });

        var meshPacket = new MeshPacket
        {
            From = 0x12345678,
            Id = 0x00FEDCBA,
            RxTime = 1_762_112_400,
            Decoded = new Data
            {
                Portnum = PortNum.NeighborinfoApp,
                Payload = neighborPayload.ToByteString()
            }
        };

        var envelope = await reader.Read("workspace-tests", "msh/US/2/e/Test/!12345678", meshPacket.ToByteArray());

        Assert.NotNull(envelope);
        Assert.Equal("Neighbor Info", envelope.PacketType);
        Assert.Equal("!12345678", envelope.FromNodeId);
        Assert.Equal("US/Test", envelope.LastHeardChannel);
        Assert.Equal("Neighbor info: 2 neighbors reported", envelope.PayloadPreview);
        Assert.NotNull(envelope.Neighbors);
        Assert.Equal(2, envelope.Neighbors.Count);
        Assert.Equal("!87654321", envelope.Neighbors[0].NodeId);
        Assert.Equal(6.0f, envelope.Neighbors[0].SnrDb);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_762_112_400), envelope.Neighbors[0].LastRxAtUtc);
        Assert.Equal("!aabbccdd", envelope.Neighbors[1].NodeId);
        Assert.Equal(-11.25f, envelope.Neighbors[1].SnrDb);
        Assert.Null(envelope.Neighbors[1].LastRxAtUtc);
    }

    [Fact]
    public async Task Read_ShouldDecodeRouting_FromDirectMeshPacket()
    {
        var reader = CreateReader();
        var routingPayload = new Routing
        {
            RouteReply = new RouteDiscovery
            {
                Route = { 0x12345678, 0x87654321 },
                SnrTowards = { 7, -4 },
                RouteBack = { 0xAABBCCDD },
                SnrBack = { 5 }
            }
        };
        var meshPacket = new MeshPacket
        {
            From = 0x12345678,
            Id = 0x01020304,
            Decoded = new Data
            {
                Portnum = PortNum.RoutingApp,
                Payload = routingPayload.ToByteString()
            }
        };

        var envelope = await reader.Read("workspace-tests", "msh/US/2/e/Test/!12345678", meshPacket.ToByteArray());

        Assert.NotNull(envelope);
        Assert.Equal("Routing", envelope.PacketType);
        Assert.Equal("!12345678", envelope.FromNodeId);
        Assert.Equal("Routing route reply: !12345678 -> !87654321; return !aabbccdd", envelope.PayloadPreview);
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

        var envelope = await reader.Read("workspace-tests", "msh/EU_868/2/json/LongFast/!1234abcd", Encoding.UTF8.GetBytes(jsonPayload));

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

        var envelope = await reader.Read("workspace-tests", "msh/US/2/json/MediumFast/!9abcdef0", Encoding.UTF8.GetBytes(jsonPayload));

        Assert.NotNull(envelope);
        Assert.Equal("Text Message", envelope.PacketType);
        Assert.Equal("Decoded text from payload", envelope.PayloadPreview);
        Assert.Equal("!9abcdef0", envelope.FromNodeId);
        Assert.Null(envelope.ToNodeId);
        Assert.Equal((uint)3405691582, envelope.PacketId);
        Assert.Equal("US/MediumFast", envelope.LastHeardChannel);
    }

    [Fact]
    public async Task Read_ShouldDecodeNeighborInfo_FromJsonDecodedPayload()
    {
        var reader = CreateReader();
        var payloadBytes = new NeighborInfo
        {
            NodeId = 0x11223344,
            LastSentById = 0x55667788,
            NodeBroadcastIntervalSecs = 3600,
            Neighbors =
            {
                new Neighbor
                {
                    NodeId = 0x55667788,
                    Snr = 3.25f
                }
            }
        }.ToByteArray();
        var jsonPayload = $$"""
                            {
                              "decoded": {
                                "portnum": "NEIGHBORINFO_APP",
                                "payload": "{{Convert.ToBase64String(payloadBytes)}}",
                                "source": 287454020,
                                "dest": 4294967295
                              },
                              "id": 19088743,
                              "timestamp": 1762112400
                            }
                            """;

        var envelope = await reader.Read("workspace-tests", "msh/US/2/json/LongFast/!11223344", Encoding.UTF8.GetBytes(jsonPayload));

        Assert.NotNull(envelope);
        Assert.Equal("Neighbor Info", envelope.PacketType);
        Assert.Equal("!11223344", envelope.FromNodeId);
        Assert.Null(envelope.ToNodeId);
        Assert.Equal("Neighbor info: 1 neighbor reported", envelope.PayloadPreview);
        Assert.NotNull(envelope.Neighbors);
        Assert.Single(envelope.Neighbors);
        Assert.Equal("!55667788", envelope.Neighbors[0].NodeId);
        Assert.Equal(3.25f, envelope.Neighbors[0].SnrDb);
    }

    [Fact]
    public async Task Read_ShouldDecodeRouting_FromJsonTypePayload()
    {
        var reader = CreateReader();
        var jsonPayload = """
                          {
                            "type": "routing",
                            "fromId": "!11223344",
                            "payload": {
                              "route_request": {
                                "route": [287454020, "!55667788"],
                                "snr_towards": [9, -3],
                                "route_back": ["!99aabbcc"]
                              }
                            }
                          }
                          """;

        var envelope = await reader.Read("workspace-tests", "msh/US/2/json/LongFast/!11223344", Encoding.UTF8.GetBytes(jsonPayload));

        Assert.NotNull(envelope);
        Assert.Equal("Routing", envelope.PacketType);
        Assert.Equal("!11223344", envelope.FromNodeId);
        Assert.Equal("Routing route request: !11223344 -> !55667788; return !99aabbcc", envelope.PayloadPreview);
    }

    [Fact]
    public async Task Read_ShouldCanonicalizeCompactBangHexNodeIds_FromJsonPayload()
    {
        var reader = CreateReader();
        var jsonPayload = """
                          {
                            "type": "text",
                            "fromId": "!999999",
                            "payload": "Hello from compact node id"
                          }
                          """;

        var envelope = await reader.Read("workspace-tests", "msh/US/2/json/LongFast/!999999", Encoding.UTF8.GetBytes(jsonPayload));

        Assert.NotNull(envelope);
        Assert.Equal("Text Message", envelope.PacketType);
        Assert.Equal("!00999999", envelope.FromNodeId);
        Assert.Equal("Hello from compact node id", envelope.PayloadPreview);
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

        var envelope = await reader.Read("workspace-tests", "msh/US/2/e/LongFast/!fa8165a4", meshPacket.ToByteArray());

        Assert.NotNull(envelope);
        Assert.Equal("Node Info", envelope.PacketType);
        Assert.Equal("Meshtastic 65a4", envelope.LongName);
        Assert.Equal("65a4", envelope.ShortName);
    }

    [Fact]
    public async Task Read_ShouldDecodeServiceEnvelope_WithNestedMeshPacket()
    {
        var reader = CreateReader();
        var payloadBytes = Encoding.UTF8.GetBytes("hello from service envelope");
        var serviceEnvelope = new ServiceEnvelope
        {
            ChannelId = "LongFast",
            GatewayId = "!cafebabe",
            Packet = new MeshPacket
            {
                From = 0x11223344,
                To = uint.MaxValue,
                Id = 0x55667788,
                Decoded = new Data
                {
                    Portnum = PortNum.TextMessageApp,
                    Payload = ByteString.CopyFrom(payloadBytes)
                }
            }
        };

        var envelope = await reader.Read("workspace-tests", "msh/US/2/e/LongFast/!11223344", serviceEnvelope.ToByteArray());

        Assert.NotNull(envelope);
        Assert.Equal("Text Message", envelope.PacketType);
        Assert.Equal("hello from service envelope", envelope.PayloadPreview);
        Assert.Equal("!11223344", envelope.FromNodeId);
        Assert.Null(envelope.ToNodeId);
        Assert.Equal((uint)0x55667788, envelope.PacketId);
    }

    [Fact]
    public async Task Read_ShouldIgnorePayload_WhenMeshPacketHasNoKnownFields()
    {
        var reader = CreateReader();

        // Protobuf field 99 (length-delimited): unknown to MeshPacket and ServiceEnvelope.
        var payload = new byte[] { 0x9A, 0x06, 0x04, 0x6E, 0x6F, 0x6F, 0x70 };

        var envelope = await reader.Read("workspace-tests", "msh/US/2/e/LongFast/!11223344", payload);

        Assert.Null(envelope);
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
            string workspaceId,
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
