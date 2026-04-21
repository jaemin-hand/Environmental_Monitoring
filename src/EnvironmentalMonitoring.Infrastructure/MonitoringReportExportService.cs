using EnvironmentalMonitoring.Domain;
using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Text;

namespace EnvironmentalMonitoring.Infrastructure;

public sealed class MonitoringReportExportService(MonitoringStorageLayout storageLayout)
{
    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = storageLayout.DatabaseFilePath,
        Mode = SqliteOpenMode.ReadOnly,
    }.ToString();

    public async Task<int> ExportDailyCsvAsync(
        DateOnly date,
        string outputPath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(storageLayout.DatabaseFilePath))
        {
            return 0;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? storageLayout.ReportDirectory);

        var rows = 0;
        var range = GetDayRange(date);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                b.sampled_at,
                b.sampling_mode,
                b.status,
                c.code,
                c.name,
                c.location_name,
                c.kind,
                c.unit,
                s.raw_value,
                s.corrected_value,
                s.quality_status
            FROM acquisition_batches b
            JOIN samples s ON s.batch_id = b.id
            JOIN channels c ON c.id = s.channel_id
            WHERE b.sampled_at >= @FromInclusive
              AND b.sampled_at < @ToExclusive
            ORDER BY b.id ASC, c.code ASC;
            """;
        command.Parameters.AddWithValue("@FromInclusive", range.FromInclusive.ToString("O"));
        command.Parameters.AddWithValue("@ToExclusive", range.ToExclusive.ToString("O"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await using var stream = new FileStream(
            outputPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false));

        await writer.WriteLineAsync(
            "sampled_at,sampling_mode,batch_status,channel_code,display_name,location_name,kind,unit,raw_value,corrected_value,quality_status");

        while (await reader.ReadAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var row = string.Join(",",
            [
                EscapeCsv(reader.GetString(0)),
                EscapeCsv(reader.GetString(1)),
                EscapeCsv(reader.GetString(2)),
                EscapeCsv(reader.GetString(3)),
                EscapeCsv(reader.GetString(4)),
                EscapeCsv(reader.IsDBNull(5) ? string.Empty : reader.GetString(5)),
                EscapeCsv(reader.GetString(6)),
                EscapeCsv(reader.GetString(7)),
                EscapeCsv(reader.IsDBNull(8) ? string.Empty : reader.GetDouble(8).ToString("0.###", CultureInfo.InvariantCulture)),
                EscapeCsv(reader.IsDBNull(9) ? string.Empty : reader.GetDouble(9).ToString("0.###", CultureInfo.InvariantCulture)),
                EscapeCsv(reader.GetString(10)),
            ]);

            await writer.WriteLineAsync(row);
            rows++;
        }

        await writer.FlushAsync();
        return rows;
    }

    public async Task<string> ExportDailyTextSummaryAsync(
        DateOnly date,
        string outputPath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(storageLayout.DatabaseFilePath))
        {
            return outputPath;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? storageLayout.ReportDirectory);

        var range = GetDayRange(date);
        var channelSummaries = new List<string>();
        var alarmCount = 0;

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using (var sampleCommand = connection.CreateCommand())
        {
            sampleCommand.CommandText = """
                SELECT
                    c.code,
                    c.name,
                    c.kind,
                    c.unit,
                    COUNT(*),
                    MIN(s.corrected_value),
                    MAX(s.corrected_value),
                    AVG(s.corrected_value)
                FROM acquisition_batches b
                JOIN samples s ON s.batch_id = b.id
                JOIN channels c ON c.id = s.channel_id
                WHERE b.sampled_at >= @FromInclusive
                  AND b.sampled_at < @ToExclusive
                  AND s.corrected_value IS NOT NULL
                GROUP BY c.code, c.name, c.kind, c.unit
                ORDER BY c.code ASC;
                """;
            sampleCommand.Parameters.AddWithValue("@FromInclusive", range.FromInclusive.ToString("O"));
            sampleCommand.Parameters.AddWithValue("@ToExclusive", range.ToExclusive.ToString("O"));

            await using var reader = await sampleCommand.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                channelSummaries.Add(
                    $"{reader.GetString(0)} / {reader.GetString(1)} / {reader.GetString(2)} / count={reader.GetInt64(4)} / min={reader.GetDouble(5):0.###}{reader.GetString(3)} / max={reader.GetDouble(6):0.###}{reader.GetString(3)} / avg={reader.GetDouble(7):0.###}{reader.GetString(3)}");
            }
        }

        await using (var alarmCommand = connection.CreateCommand())
        {
            alarmCommand.CommandText = """
                SELECT COUNT(*)
                FROM alarm_events
                WHERE occurred_at >= @FromInclusive
                  AND occurred_at < @ToExclusive;
                """;
            alarmCommand.Parameters.AddWithValue("@FromInclusive", range.FromInclusive.ToString("O"));
            alarmCommand.Parameters.AddWithValue("@ToExclusive", range.ToExclusive.ToString("O"));

            var result = await alarmCommand.ExecuteScalarAsync(cancellationToken);
            alarmCount = Convert.ToInt32(result, CultureInfo.InvariantCulture);
        }

        var report = new StringBuilder();
        report.AppendLine($"Environmental Monitoring Daily Summary");
        report.AppendLine($"Date: {date:yyyy-MM-dd}");
        report.AppendLine($"Alarm Count: {alarmCount}");
        report.AppendLine();
        report.AppendLine("Channel Statistics");

        if (channelSummaries.Count == 0)
        {
            report.AppendLine("No samples.");
        }
        else
        {
            foreach (var line in channelSummaries)
            {
                report.AppendLine(line);
            }
        }

        await File.WriteAllTextAsync(outputPath, report.ToString(), new UTF8Encoding(false), cancellationToken);
        return outputPath;
    }

    private static (DateTimeOffset FromInclusive, DateTimeOffset ToExclusive) GetDayRange(DateOnly date)
    {
        var from = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeZoneInfo.Local.GetUtcOffset(DateTime.Now));
        var to = from.AddDays(1);
        return (from, to);
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
