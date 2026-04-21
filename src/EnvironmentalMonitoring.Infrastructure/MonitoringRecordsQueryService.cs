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
        DateOnly? sampledOnDate,
        string? channelCode,
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
        var filters = new List<string>();

        if (sampledOnDate.HasValue)
        {
            var dateStart = CreateLocalBoundary(sampledOnDate.Value);
            filters.Add("b.sampled_at >= @DateStart AND b.sampled_at < @DateEnd");
            command.Parameters.AddWithValue("@DateStart", dateStart.ToString("O"));
            command.Parameters.AddWithValue("@DateEnd", dateStart.AddDays(1).ToString("O"));
        }

        if (!string.IsNullOrWhiteSpace(channelCode))
        {
            filters.Add("c.code = @ChannelCode");
            command.Parameters.AddWithValue("@ChannelCode", channelCode);
        }

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
            """;

        if (filters.Count > 0)
        {
            command.CommandText += Environment.NewLine
                + "WHERE "
                + string.Join(Environment.NewLine + "  AND ", filters)
                + Environment.NewLine;
        }

        command.CommandText += """
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
        DateOnly? occurredOnDate,
        string? channelCode,
        bool activeOnly,
        bool unacknowledgedOnly,
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
        var filters = new List<string>();

        if (occurredOnDate.HasValue)
        {
            var dateStart = CreateLocalBoundary(occurredOnDate.Value);
            filters.Add("a.occurred_at >= @DateStart AND a.occurred_at < @DateEnd");
            command.Parameters.AddWithValue("@DateStart", dateStart.ToString("O"));
            command.Parameters.AddWithValue("@DateEnd", dateStart.AddDays(1).ToString("O"));
        }

        if (!string.IsNullOrWhiteSpace(channelCode))
        {
            filters.Add("c.code = @ChannelCode");
            command.Parameters.AddWithValue("@ChannelCode", channelCode);
        }

        if (unacknowledgedOnly)
        {
            filters.Add("a.acknowledged_at IS NULL");
            filters.Add("a.resolved_at IS NULL");
        }
        else if (activeOnly)
        {
            filters.Add("a.resolved_at IS NULL");
        }

        command.CommandText = """
            SELECT
                a.id,
                a.occurred_at,
                a.acknowledged_at,
                a.resolved_at,
                c.code,
                a.alarm_type,
                a.severity,
                a.measured_value,
                a.message
            FROM alarm_events a
            JOIN channels c ON c.id = a.channel_id
            """;

        if (filters.Count > 0)
        {
            command.CommandText += Environment.NewLine
                + "WHERE "
                + string.Join(Environment.NewLine + "  AND ", filters)
                + Environment.NewLine;
        }

        command.CommandText += """
            ORDER BY a.occurred_at DESC, a.id DESC
            LIMIT @Limit;
            """;
        command.Parameters.AddWithValue("@Limit", limit);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new MonitoringAlarmRecord(
                reader.GetInt64(0),
                DateTimeOffset.Parse(reader.GetString(1)),
                reader.IsDBNull(2) ? null : DateTimeOffset.Parse(reader.GetString(2)),
                reader.IsDBNull(3) ? null : DateTimeOffset.Parse(reader.GetString(3)),
                reader.GetString(4),
                reader.GetString(5),
                ParseSeverity(reader.GetString(6)),
                reader.IsDBNull(7) ? null : reader.GetDouble(7),
                reader.GetString(8)));
        }

        return records;
    }

    private static DateTimeOffset CreateLocalBoundary(DateOnly date)
    {
        var localDateTime = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local);
        return new DateTimeOffset(localDateTime);
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
