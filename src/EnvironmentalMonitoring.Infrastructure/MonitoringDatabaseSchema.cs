namespace EnvironmentalMonitoring.Infrastructure;

internal static class MonitoringDatabaseSchema
{
    public const string Sql = """
        CREATE TABLE IF NOT EXISTS devices (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            code TEXT NOT NULL UNIQUE,
            name TEXT NOT NULL,
            protocol TEXT NOT NULL,
            ip_address TEXT,
            port INTEGER,
            is_active INTEGER NOT NULL DEFAULT 1
        );

        CREATE TABLE IF NOT EXISTS channels (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            code TEXT NOT NULL UNIQUE,
            name TEXT NOT NULL,
            kind TEXT NOT NULL,
            unit TEXT NOT NULL,
            device_id INTEGER NOT NULL,
            device_channel_no INTEGER NOT NULL,
            location_name TEXT,
            is_active INTEGER NOT NULL DEFAULT 1,
            FOREIGN KEY (device_id) REFERENCES devices(id)
        );

        CREATE TABLE IF NOT EXISTS acquisition_batches (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            sampled_at TEXT NOT NULL,
            sampling_mode TEXT NOT NULL,
            status TEXT NOT NULL,
            created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
        );

        CREATE TABLE IF NOT EXISTS samples (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            batch_id INTEGER NOT NULL,
            channel_id INTEGER NOT NULL,
            raw_value REAL,
            corrected_value REAL,
            quality_status TEXT NOT NULL,
            created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
            FOREIGN KEY (batch_id) REFERENCES acquisition_batches(id),
            FOREIGN KEY (channel_id) REFERENCES channels(id)
        );

        CREATE TABLE IF NOT EXISTS alarm_events (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            channel_id INTEGER NOT NULL,
            batch_id INTEGER,
            alarm_type TEXT NOT NULL,
            severity TEXT NOT NULL,
            measured_value REAL,
            message TEXT NOT NULL,
            occurred_at TEXT NOT NULL,
            acknowledged_at TEXT,
            resolved_at TEXT,
            FOREIGN KEY (channel_id) REFERENCES channels(id),
            FOREIGN KEY (batch_id) REFERENCES acquisition_batches(id)
        );

        CREATE TABLE IF NOT EXISTS app_settings (
            key TEXT PRIMARY KEY,
            value TEXT NOT NULL,
            updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
        );

        CREATE INDEX IF NOT EXISTS idx_samples_channel_created_at
        ON samples(channel_id, created_at);

        CREATE INDEX IF NOT EXISTS idx_batches_sampled_at
        ON acquisition_batches(sampled_at);

        CREATE INDEX IF NOT EXISTS idx_alarm_events_occurred_at
        ON alarm_events(occurred_at);
        """;
}
