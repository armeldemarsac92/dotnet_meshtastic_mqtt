namespace MeshBoard.Infrastructure.Persistence.SQL;

internal static class NodeQueries
{
    public static string GetNodes =>
        """
        SELECT
            node_id AS NodeId,
            short_name AS ShortName,
            long_name AS LongName,
            last_heard_at_utc AS LastHeardAtUtc,
            last_text_message_at_utc AS LastTextMessageAtUtc,
            last_known_latitude AS LastKnownLatitude,
            last_known_longitude AS LastKnownLongitude,
            battery_level_percent AS BatteryLevelPercent,
            voltage AS Voltage,
            channel_utilization AS ChannelUtilization,
            air_util_tx AS AirUtilTx,
            uptime_seconds AS UptimeSeconds,
            temperature_celsius AS TemperatureCelsius,
            relative_humidity AS RelativeHumidity,
            barometric_pressure AS BarometricPressure
        FROM nodes
        ORDER BY COALESCE(long_name, short_name, node_id);
        """;

    public static string UpsertNode =>
        """
        INSERT INTO nodes (
            node_id,
            short_name,
            long_name,
            last_heard_at_utc,
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
            @ShortName,
            @LongName,
            @LastHeardAtUtc,
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
            short_name = COALESCE(excluded.short_name, nodes.short_name),
            long_name = COALESCE(excluded.long_name, nodes.long_name),
            last_heard_at_utc = COALESCE(excluded.last_heard_at_utc, nodes.last_heard_at_utc),
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
}
