using EnvironmentalMonitoring.Domain;
using Microsoft.Data.Sqlite;

namespace EnvironmentalMonitoring.Infrastructure;

public sealed class MonitoringRecordsQueryService(MonitoringStorageLayout storageLayout)
{
    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = storageLayout.DatabaseFilePath,
        Mode = SqliteOpenMode.ReadOnly,
    }.ToString();

    public async Task<IReadOnlyList<MonitoringSampleRecord>> GetRecentSamplesAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(storageLayout.DatabaseFilePath))
        {
            return [];
        }

        var records = new List<MonitoringSampleRecord>(limit);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                b.sampled_at,
                c.code,
                c.kind,
                c.unit,
                s.raw_value,
                s.corrected_value,
                s.quality_status
            FROM acquisition_batches b
            JOIN samples s ON s.batch_id = b.id
            JOIN channels c ON c.id = s.channel_id
            ORDER BY b.id DESC, c.code ASC
            LIMIT @Limit;
            """;
        command.Parameters.AddWithValue("@Limit", limit);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new MonitoringSampleRecord(
                DateTimeOffset.Parse(reader.GetString(0)),
                reader.GetString(1),
                Enum.Parse<ChannelKind>(reader.GetString(2)),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetDouble(4),
                reader.IsDBNull(5) ? null : reader.GetDouble(5),
                Enum.Parse<SampleQualityStatus>(reader.GetString(6))));
        }

        return records;
    }

    public async Task<IReadOnlyList<MonitoringAlarmRecord>> GetAlarmHistoryAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(storageLayout.DatabaseFilePath))
        {
            return [];
        }

        var records = new List<MonitoringAlarmRecord>(limit);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                a.occurred_at,
                a.resolved_at,
                c.code,
                a.alarm_type,
                a.severity,
                a.measured_value,
                a.message
            FROM alarm_events a
            JOIN channels c ON c.id = a.channel_id
            ORDER BY a.occurred_at DESC, a.id DESC
            LIMIT @Limit;
            """;
        command.Parameters.AddWithValue("@Limit", limit);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new MonitoringAlarmRecord(
                DateTimeOffset.Parse(reader.GetString(0)),
                reader.IsDBNull(1) ? null : DateTimeOffset.Parse(reader.GetString(1)),
                reader.GetString(2),
                reader.GetString(3),
                ParseSeverity(reader.GetString(4)),
                reader.IsDBNull(5) ? null : reader.GetDouble(5),
                reader.GetString(6)));
        }

        return records;
    }

    private static MonitoringEventSeverity ParseSeverity(string severity) =>
        severity.ToUpperInvariant() switch
        {
            "CRITICAL" => MonitoringEventSeverity.Critical,
            "ERROR" => MonitoringEventSeverity.Critical,
            "WARNING" => MonitoringEventSeverity.Warning,
            _ => MonitoringEventSeverity.Info,
        };
}
