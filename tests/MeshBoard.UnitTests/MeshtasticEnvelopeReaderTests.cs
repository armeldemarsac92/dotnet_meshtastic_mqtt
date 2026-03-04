using Google.Protobuf;
using MeshBoard.Infrastructure.Meshtastic.Decoding;
using MeshBoard.Infrastructure.Meshtastic.Protobuf;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeshBoard.UnitTests;

public sealed class MeshtasticEnvelopeReaderTests
{
    [Fact]
    public async Task Read_ShouldDecodeDeviceTelemetry_FromDirectMeshPacket()
    {
        var reader = new MeshtasticEnvelopeReader(NullLogger<MeshtasticEnvelopeReader>.Instance);
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
        var reader = new MeshtasticEnvelopeReader(NullLogger<MeshtasticEnvelopeReader>.Instance);
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
}
