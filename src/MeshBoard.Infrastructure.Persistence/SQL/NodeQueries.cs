namespace MeshBoard.Infrastructure.Persistence.SQL;

internal static class NodeQueries
{
    public static string CountNodes =>
        $"""
        SELECT COUNT(1)
        {FromAndWhereClause}
        """;

    public static string SelectNodes =>
        $"""
        SELECT
            n.node_id AS NodeId,
            COALESCE(n.broker_server, 'unknown') AS BrokerServer,
            n.short_name AS ShortName,
            n.long_name AS LongName,
            n.last_heard_at_utc AS LastHeardAtUtc,
            n.last_heard_channel AS LastHeardChannel,
            n.last_text_message_at_utc AS LastTextMessageAtUtc,
            n.last_known_latitude AS LastKnownLatitude,
            n.last_known_longitude AS LastKnownLongitude,
            n.battery_level_percent AS BatteryLevelPercent,
            n.voltage AS Voltage,
            n.channel_utilization AS ChannelUtilization,
            n.air_util_tx AS AirUtilTx,
            n.uptime_seconds AS UptimeSeconds,
            n.temperature_celsius AS TemperatureCelsius,
            n.relative_humidity AS RelativeHumidity,
            n.barometric_pressure AS BarometricPressure
        {FromAndWhereClause}
        """;

    public static string UpsertNode =>
        """
        INSERT INTO nodes (
            node_id,
            broker_server,
            short_name,
            long_name,
            last_heard_at_utc,
            last_heard_channel,
            last_text_message_at_utc,
            last_known_latitude,
            last_known_longitude,
            battery_level_percent,
            voltage,
            channel_utilization,
            air_util_tx,
            uptime_seconds,
            temperature_celsius,
            relative_humidity,
            barometric_pressure)
        VALUES (
            @NodeId,
            COALESCE(NULLIF(@BrokerServer, ''), 'unknown'),
            @ShortName,
            @LongName,
            @LastHeardAtUtc,
            @LastHeardChannel,
            @LastTextMessageAtUtc,
            @LastKnownLatitude,
            @LastKnownLongitude,
            @BatteryLevelPercent,
            @Voltage,
            @ChannelUtilization,
            @AirUtilTx,
            @UptimeSeconds,
            @TemperatureCelsius,
            @RelativeHumidity,
            @BarometricPressure)
        ON CONFLICT(node_id) DO UPDATE SET
            broker_server = COALESCE(NULLIF(excluded.broker_server, ''), nodes.broker_server),
            short_name = COALESCE(excluded.short_name, nodes.short_name),
            long_name = COALESCE(excluded.long_name, nodes.long_name),
            last_heard_at_utc = COALESCE(excluded.last_heard_at_utc, nodes.last_heard_at_utc),
            last_heard_channel = COALESCE(excluded.last_heard_channel, nodes.last_heard_channel),
            last_text_message_at_utc = COALESCE(excluded.last_text_message_at_utc, nodes.last_text_message_at_utc),
            last_known_latitude = COALESCE(excluded.last_known_latitude, nodes.last_known_latitude),
            last_known_longitude = COALESCE(excluded.last_known_longitude, nodes.last_known_longitude),
            battery_level_percent = COALESCE(excluded.battery_level_percent, nodes.battery_level_percent),
            voltage = COALESCE(excluded.voltage, nodes.voltage),
            channel_utilization = COALESCE(excluded.channel_utilization, nodes.channel_utilization),
            air_util_tx = COALESCE(excluded.air_util_tx, nodes.air_util_tx),
            uptime_seconds = COALESCE(excluded.uptime_seconds, nodes.uptime_seconds),
            temperature_celsius = COALESCE(excluded.temperature_celsius, nodes.temperature_celsius),
            relative_humidity = COALESCE(excluded.relative_humidity, nodes.relative_humidity),
            barometric_pressure = COALESCE(excluded.barometric_pressure, nodes.barometric_pressure);
        """;

    private static string FromAndWhereClause =>
        """
        FROM nodes n
        LEFT JOIN favorite_nodes f
            ON f.node_id = n.node_id
        WHERE (
            @SearchText = '' OR
            n.node_id LIKE @SearchPattern OR
            COALESCE(n.short_name, '') LIKE @SearchPattern OR
            COALESCE(n.long_name, '') LIKE @SearchPattern OR
            COALESCE(n.last_heard_channel, '') LIKE @SearchPattern
        )
        AND (
            @OnlyWithLocation = 0 OR
            (n.last_known_latitude IS NOT NULL AND n.last_known_longitude IS NOT NULL)
        )
        AND (
            @OnlyWithTelemetry = 0 OR
            n.battery_level_percent IS NOT NULL OR
            n.voltage IS NOT NULL OR
            n.channel_utilization IS NOT NULL OR
            n.air_util_tx IS NOT NULL OR
            n.uptime_seconds IS NOT NULL OR
            n.temperature_celsius IS NOT NULL OR
            n.relative_humidity IS NOT NULL OR
            n.barometric_pressure IS NOT NULL
        )
        AND (
            @OnlyFavorites = 0 OR
            f.node_id IS NOT NULL
        )
        """;
}
