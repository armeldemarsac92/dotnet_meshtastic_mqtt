using MeshBoard.Application.Abstractions.Meshtastic;

namespace MeshBoard.Infrastructure.Meshtastic.Mqtt;

internal interface IMqttSessionFactory
{
    IMqttSession Create(MqttSessionConnectionSettings settings);
}
