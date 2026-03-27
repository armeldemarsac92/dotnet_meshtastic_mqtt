SELECT create_hypertable(
    'collector_channel_packet_hourly_rollups',
    by_range('bucket_start_utc', INTERVAL '7 days'),
    migrate_data => true,
    if_not_exists => true
);

SELECT create_hypertable(
    'collector_node_packet_hourly_rollups',
    by_range('bucket_start_utc', INTERVAL '7 days'),
    migrate_data => true,
    if_not_exists => true
);

SELECT create_hypertable(
    'collector_neighbor_link_hourly_rollups',
    by_range('bucket_start_utc', INTERVAL '7 days'),
    migrate_data => true,
    if_not_exists => true
);
