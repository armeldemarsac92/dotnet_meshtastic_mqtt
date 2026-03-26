using MassTransit;
using MeshBoard.Contracts.CollectorEvents;
using MeshBoard.Contracts.CollectorEvents.Normalized;
using MeshBoard.Contracts.CollectorEvents.RawPackets;
using MeshBoard.Infrastructure.Eventing.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MeshBoard.Infrastructure.Eventing.DependencyInjection;

public static class ServiceCollectionExtensions
{
    private static readonly string[] CollectorTopicNames =
    [
        CollectorEventTopicNames.RawPackets,
        CollectorEventTopicNames.PacketNormalized,
        CollectorEventTopicNames.NodeObserved,
        CollectorEventTopicNames.LinkObserved,
        CollectorEventTopicNames.TelemetryObserved,
        CollectorEventTopicNames.DeadLetter
    ];

    public static IServiceCollection AddCollectorEventingInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IRiderRegistrationConfigurator> configureConsumers)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configureConsumers);

        AddKafkaOptions(services, configuration);
        var bootstrapServers = GetBootstrapServers(configuration);

        EnsureBootstrapServers(bootstrapServers);
        EnsureCollectorTopicNamesAreUnique();

        services.AddMassTransit(busRegistrationConfigurator =>
        {
            busRegistrationConfigurator.UsingInMemory();

            busRegistrationConfigurator.AddRider(riderRegistrationConfigurator =>
            {
                AddCollectorTopicProducers(riderRegistrationConfigurator);
                configureConsumers(riderRegistrationConfigurator);

                riderRegistrationConfigurator.UsingKafka((context, kafkaConfigurator) =>
                {
                    kafkaConfigurator.Host(bootstrapServers);
                    ConfigureCollectorTopicEndpoints(context, kafkaConfigurator);
                });
            });
        });

        return services;
    }

    private static void AddKafkaOptions(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<KafkaOptions>()
            .Bind(configuration.GetSection(KafkaOptions.SectionName));
    }

    private static void AddCollectorTopicProducers(IRiderRegistrationConfigurator riderRegistrationConfigurator)
    {
        riderRegistrationConfigurator.AddProducer<RawPacketReceived>(CollectorEventTopicNames.RawPackets);
        riderRegistrationConfigurator.AddProducer<PacketNormalized>(CollectorEventTopicNames.PacketNormalized);
        riderRegistrationConfigurator.AddProducer<NodeObserved>(CollectorEventTopicNames.NodeObserved);
        riderRegistrationConfigurator.AddProducer<LinkObserved>(CollectorEventTopicNames.LinkObserved);
        riderRegistrationConfigurator.AddProducer<TelemetryObserved>(CollectorEventTopicNames.TelemetryObserved);
    }

    private static void ConfigureCollectorTopicEndpoints(
        IRiderRegistrationContext context,
        IKafkaFactoryConfigurator kafkaConfigurator)
    {
        var registrations = context
            .GetServices<CollectorKafkaTopicConsumerRegistration>()
            .GroupBy(registration => new CollectorKafkaTopicEndpointKey(
                registration.MessageType,
                registration.TopicName,
                registration.ConsumerGroup));

        foreach (var registrationGroup in registrations)
        {
            ConfigureCollectorTopicEndpoint(context, kafkaConfigurator, registrationGroup);
        }
    }

    private static void ConfigureCollectorTopicEndpoint(
        IRiderRegistrationContext context,
        IKafkaFactoryConfigurator kafkaConfigurator,
        IGrouping<CollectorKafkaTopicEndpointKey, CollectorKafkaTopicConsumerRegistration> registrationGroup)
    {
        var endpointKey = registrationGroup.Key;

        if (endpointKey.MessageType == typeof(RawPacketReceived))
        {
            ConfigureCollectorTopicEndpoint<RawPacketReceived>(context, kafkaConfigurator, endpointKey, registrationGroup);
            return;
        }

        if (endpointKey.MessageType == typeof(PacketNormalized))
        {
            ConfigureCollectorTopicEndpoint<PacketNormalized>(context, kafkaConfigurator, endpointKey, registrationGroup);
            return;
        }

        if (endpointKey.MessageType == typeof(NodeObserved))
        {
            ConfigureCollectorTopicEndpoint<NodeObserved>(context, kafkaConfigurator, endpointKey, registrationGroup);
            return;
        }

        if (endpointKey.MessageType == typeof(LinkObserved))
        {
            ConfigureCollectorTopicEndpoint<LinkObserved>(context, kafkaConfigurator, endpointKey, registrationGroup);
            return;
        }

        if (endpointKey.MessageType == typeof(TelemetryObserved))
        {
            ConfigureCollectorTopicEndpoint<TelemetryObserved>(context, kafkaConfigurator, endpointKey, registrationGroup);
            return;
        }

        throw new NotSupportedException(
            $"The collector event message type '{endpointKey.MessageType.FullName}' is not supported for Kafka topic endpoint configuration.");
    }

    private static void ConfigureCollectorTopicEndpoint<TMessage>(
        IRiderRegistrationContext context,
        IKafkaFactoryConfigurator kafkaConfigurator,
        CollectorKafkaTopicEndpointKey endpointKey,
        IEnumerable<CollectorKafkaTopicConsumerRegistration> registrations)
        where TMessage : class
    {
        kafkaConfigurator.TopicEndpoint<TMessage>(endpointKey.TopicName, endpointKey.ConsumerGroup, endpointConfigurator =>
        {
            foreach (var registration in registrations)
            {
                endpointConfigurator.ConfigureConsumer(context, registration.ConsumerType);
            }
        });
    }

    private static string GetBootstrapServers(IConfiguration configuration)
    {
        return configuration
            .GetSection(KafkaOptions.SectionName)
            .GetValue<string>(nameof(KafkaOptions.BootstrapServers))
            ?.Trim() ?? string.Empty;
    }

    private static void EnsureBootstrapServers(string bootstrapServers)
    {
        if (string.IsNullOrWhiteSpace(bootstrapServers))
        {
            throw new InvalidOperationException(
                $"The configured Kafka bootstrap servers are missing. Set '{KafkaOptions.SectionName}:{nameof(KafkaOptions.BootstrapServers)}'.");
        }
    }

    private static void EnsureCollectorTopicNamesAreUnique()
    {
        if (CollectorTopicNames.Length != CollectorTopicNames.Distinct(StringComparer.Ordinal).Count())
        {
            throw new InvalidOperationException("Collector event topic names must be unique.");
        }
    }
}

public static class CollectorTopicConfigurationExtensions
{
    public static IRiderRegistrationConfigurator AddCollectorRawPacketsConsumer<TConsumer>(
        this IRiderRegistrationConfigurator riderRegistrationConfigurator,
        string consumerGroup)
        where TConsumer : class, IConsumer<RawPacketReceived>
    {
        return AddCollectorTopicConsumer<TConsumer, RawPacketReceived>(
            riderRegistrationConfigurator,
            CollectorEventTopicNames.RawPackets,
            consumerGroup);
    }

    public static IRiderRegistrationConfigurator AddCollectorPacketNormalizedConsumer<TConsumer>(
        this IRiderRegistrationConfigurator riderRegistrationConfigurator,
        string consumerGroup)
        where TConsumer : class, IConsumer<PacketNormalized>
    {
        return AddCollectorTopicConsumer<TConsumer, PacketNormalized>(
            riderRegistrationConfigurator,
            CollectorEventTopicNames.PacketNormalized,
            consumerGroup);
    }

    public static IRiderRegistrationConfigurator AddCollectorNodeObservedConsumer<TConsumer>(
        this IRiderRegistrationConfigurator riderRegistrationConfigurator,
        string consumerGroup)
        where TConsumer : class, IConsumer<NodeObserved>
    {
        return AddCollectorTopicConsumer<TConsumer, NodeObserved>(
            riderRegistrationConfigurator,
            CollectorEventTopicNames.NodeObserved,
            consumerGroup);
    }

    public static IRiderRegistrationConfigurator AddCollectorLinkObservedConsumer<TConsumer>(
        this IRiderRegistrationConfigurator riderRegistrationConfigurator,
        string consumerGroup)
        where TConsumer : class, IConsumer<LinkObserved>
    {
        return AddCollectorTopicConsumer<TConsumer, LinkObserved>(
            riderRegistrationConfigurator,
            CollectorEventTopicNames.LinkObserved,
            consumerGroup);
    }

    public static IRiderRegistrationConfigurator AddCollectorTelemetryObservedConsumer<TConsumer>(
        this IRiderRegistrationConfigurator riderRegistrationConfigurator,
        string consumerGroup)
        where TConsumer : class, IConsumer<TelemetryObserved>
    {
        return AddCollectorTopicConsumer<TConsumer, TelemetryObserved>(
            riderRegistrationConfigurator,
            CollectorEventTopicNames.TelemetryObserved,
            consumerGroup);
    }

    private static IRiderRegistrationConfigurator AddCollectorTopicConsumer<TConsumer, TMessage>(
        IRiderRegistrationConfigurator riderRegistrationConfigurator,
        string topicName,
        string consumerGroup)
        where TConsumer : class, IConsumer<TMessage>
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(riderRegistrationConfigurator);

        if (string.IsNullOrWhiteSpace(consumerGroup))
        {
            throw new ArgumentException("A non-empty Kafka consumer group is required.", nameof(consumerGroup));
        }

        riderRegistrationConfigurator.AddConsumer<TConsumer>();
        riderRegistrationConfigurator.AddSingleton(new CollectorKafkaTopicConsumerRegistration(
            typeof(TMessage),
            topicName,
            consumerGroup.Trim(),
            typeof(TConsumer)));

        return riderRegistrationConfigurator;
    }
}

internal sealed record CollectorKafkaTopicConsumerRegistration(
    Type MessageType,
    string TopicName,
    string ConsumerGroup,
    Type ConsumerType);

internal readonly record struct CollectorKafkaTopicEndpointKey(
    Type MessageType,
    string TopicName,
    string ConsumerGroup);
