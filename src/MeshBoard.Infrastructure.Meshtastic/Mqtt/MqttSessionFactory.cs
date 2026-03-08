using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Contracts.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MeshBoard.Infrastructure.Meshtastic.Mqtt;

internal sealed class MqttSessionFactory : IMqttSessionFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly IOptions<BrokerOptions> _brokerOptions;

    public MqttSessionFactory(ILoggerFactory loggerFactory, IOptions<BrokerOptions> brokerOptions)
    {
        _loggerFactory = loggerFactory;
        _brokerOptions = brokerOptions;
    }

    public IMqttSession Create(MqttSessionConnectionSettings settings)
    {
        return new MqttSession(
            settings,
            _brokerOptions,
            _loggerFactory.CreateLogger<MqttSession>());
    }
}
