using EnvironmentalMonitoring.Domain;
using Microsoft.Data.Sqlite;
using System.Globalization;

namespace EnvironmentalMonitoring.Infrastructure;

public sealed class MonitoringDashboardQueryService(
    MonitoringStorageLayout storageLayout,
    MonitoringBlueprint blueprint)
{
    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = storageLayout.DatabaseFilePath,
        Mode = SqliteOpenMode.ReadOnly,
    }.ToString();

    public async Task<MonitoringDashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(storageLayout.DatabaseFilePath))
        {
            return CreateEmptySnapshot();
        }

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var latestBatch = await GetLatestBatchAsync(connection, cancellationToken);
        var activeAlarmSummary = await GetActiveAlarmSummaryAsync(connection, cancellationToken);
        var channelSnapshots = await GetLatestChannelSnapshotsAsync(connection, cancellationToken);
        var trendPoints = await GetTrendPointsAsync(connection, cancellationToken);
        var recentEvents = await GetRecentEventsAsync(connection, latestBatch, channelSnapshots, cancellationToken);

        return new MonitoringDashboardSnapshot(
            CreateStorageStatus(latestBatch),
            activeAlarmSummary.Count,
            activeAlarmSummary.HighestSeverity,
            channelSnapshots,
            trendPoints,
            recentEvents);
    }

    private MonitoringDashboardSnapshot CreateEmptySnapshot()
    {
        var channels = blueprint.Channels
            .Select(channel => new MonitoringChannelSnapshot(
                channel.Name,
                channel.Kind,
                channel.Unit,
                null,
                SampleQualityStatus.CommunicationError,
                null))
            .ToArray();

        return new MonitoringDashboardSnapshot(
            new StorageStatusSnapshot(
                StorageHealth.Delayed,
                null,
                0,
                "대기",
                "저장된 데이터가 아직 없습니다."),
            0,
            MonitoringEventSeverity.Info,
            channels,
            [],
            [
                new MonitoringEventSnapshot(
                    DateTimeOffset.Now,
                    "데이터베이스 파일이 아직 생성되지 않았습니다.",
                    MonitoringEventSeverity.Info),
            ]);
    }

    private static async Task<(DateTimeOffset SampledAt, DateTimeOffset PersistedAt, SamplingMode SamplingMode, AcquisitionBatchStatus Status)?>
        GetLatestBatchAsync(
            SqliteConnection connection,
            CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT sampled_at, created_at, sampling_mode, status
            FROM acquisition_batches
            ORDER BY id DESC
            LIMIT 1;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return (
            DateTimeOffset.Parse(reader.GetString(0), CultureInfo.InvariantCulture),
            ParseSqliteUtcTimestamp(reader.GetString(1)),
            Enum.Parse<SamplingMode>(reader.GetString(2)),
            Enum.Parse<AcquisitionBatchStatus>(reader.GetString(3)));
    }

    private static async Task<(int Count, MonitoringEventSeverity HighestSeverity)> GetActiveAlarmSummaryAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var count = 0;
        var highestSeverity = MonitoringEventSeverity.Info;

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT severity, COUNT(*)
            FROM alarm_events
            WHERE resolved_at IS NULL
            GROUP BY severity;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var severity = ParseEventSeverity(reader.GetString(0));
            count += reader.GetInt32(1);

            if (severity > highestSeverity)
            {
                highestSeverity = severity;
            }
        }

        return (count, highestSeverity);
    }

    private async Task<IReadOnlyList<MonitoringChannelSnapshot>> GetLatestChannelSnapshotsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var latestBatchChannels = new Dictionary<string, MonitoringChannelSnapshot>(StringComparer.OrdinalIgnoreCase);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                c.code,
                c.kind,
                c.unit,
                s.corrected_value,
                s.quality_status,
                b.sampled_at
            FROM acquisition_batches b
            JOIN samples s ON s.batch_id = b.id
            JOIN channels c ON c.id = s.channel_id
            WHERE b.id = (
                SELECT id
                FROM acquisition_batches
                ORDER BY id DESC
                LIMIT 1
            )
            ORDER BY c.code;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var channelCode = reader.GetString(0);
            latestBatchChannels[channelCode] = new MonitoringChannelSnapshot(
                channelCode,
                Enum.Parse<ChannelKind>(reader.GetString(1)),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetDouble(3),
                Enum.Parse<SampleQualityStatus>(reader.GetString(4)),
                DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture));
        }

        return blueprint.Channels
            .Select(channel => latestBatchChannels.TryGetValue(channel.Name, out var snapshot)
                ? snapshot
                : new MonitoringChannelSnapshot(
                    channel.Name,
                    channel.Kind,
                    channel.Unit,
                    null,
                    SampleQualityStatus.CommunicationError,
                    null))
            .ToArray();
    }

    private static async Task<IReadOnlyList<MonitoringTrendPoint>> GetTrendPointsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var points = new List<MonitoringTrendPoint>();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                b.sampled_at,
                AVG(CASE WHEN c.kind = 'Temperature' THEN s.corrected_value END) AS avg_temperature,
                MAX(CASE WHEN c.kind = 'Humidity' THEN s.corrected_value END) AS humidity
            FROM acquisition_batches b
            JOIN samples s ON s.batch_id = b.id
            JOIN channels c ON c.id = s.channel_id
            GROUP BY b.id, b.sampled_at
            ORDER BY b.id DESC
            LIMIT 24;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            points.Add(new MonitoringTrendPoint(
                DateTimeOffset.Parse(reader.GetString(0), CultureInfo.InvariantCulture),
                reader.IsDBNull(1) ? 0.0 : reader.GetDouble(1),
                reader.IsDBNull(2) ? null : reader.GetDouble(2)));
        }

        points.Reverse();
        return points;
    }

    private static async Task<IReadOnlyList<MonitoringEventSnapshot>> GetRecentEventsAsync(
        SqliteConnection connection,
        (DateTimeOffset SampledAt, DateTimeOffset PersistedAt, SamplingMode SamplingMode, AcquisitionBatchStatus Status)? latestBatch,
        IReadOnlyList<MonitoringChannelSnapshot> channelSnapshots,
        CancellationToken cancellationToken)
    {
        var events = new List<MonitoringEventSnapshot>();

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT occurred_at, message, severity
                FROM alarm_events
                ORDER BY occurred_at DESC, id DESC
                LIMIT 3;
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                events.Add(new MonitoringEventSnapshot(
                    DateTimeOffset.Parse(reader.GetString(0), CultureInfo.InvariantCulture),
                    reader.GetString(1),
                    ParseEventSeverity(reader.GetString(2))));
            }
        }

        if (events.Count == 0)
        {
            events.AddRange(channelSnapshots
                .Where(item => item.QualityStatus != SampleQualityStatus.Normal && item.SampledAt is not null)
                .Take(3)
                .Select(item => new MonitoringEventSnapshot(
                    item.SampledAt!.Value,
                    $"{item.ChannelCode} 통신 또는 품질 이상",
                    MonitoringEventSeverity.Warning)));
        }

        if (events.Count == 0 && latestBatch is not null)
        {
            events.Add(new MonitoringEventSnapshot(
                latestBatch.Value.PersistedAt.ToLocalTime(),
                $"최근 저장 완료 / 배치 상태 {latestBatch.Value.Status}",
                MonitoringEventSeverity.Info));

            events.Add(new MonitoringEventSnapshot(
                latestBatch.Value.SampledAt.ToLocalTime(),
                "최신 샘플 조회 성공",
                MonitoringEventSeverity.Info));
        }

        return events
            .OrderByDescending(item => item.OccurredAt)
            .Take(3)
            .ToArray();
    }

    private static MonitoringEventSeverity ParseEventSeverity(string severity) =>
        severity.ToUpperInvariant() switch
        {
            "CRITICAL" => MonitoringEventSeverity.Critical,
            "ERROR" => MonitoringEventSeverity.Critical,
            "WARNING" => MonitoringEventSeverity.Warning,
            _ => MonitoringEventSeverity.Info,
        };

    private static StorageStatusSnapshot CreateStorageStatus(
        (DateTimeOffset SampledAt, DateTimeOffset PersistedAt, SamplingMode SamplingMode, AcquisitionBatchStatus Status)? latestBatch)
    {
        if (latestBatch is null)
        {
            return new StorageStatusSnapshot(
                StorageHealth.Delayed,
                null,
                0,
                "대기",
                "저장된 샘플이 없습니다.");
        }

        var now = DateTimeOffset.Now;
        var lastSuccessfulWriteAt = latestBatch.Value.PersistedAt.ToLocalTime();
        var samplingSeconds = (int)latestBatch.Value.SamplingMode;
        var healthyThreshold = TimeSpan.FromSeconds(Math.Max(1, samplingSeconds + 1));
        var errorThreshold = TimeSpan.FromSeconds(Math.Max(3, samplingSeconds * 3));
        var age = now - lastSuccessfulWriteAt;

        if (age <= healthyThreshold)
        {
            return new StorageStatusSnapshot(
                StorageHealth.Healthy,
                lastSuccessfulWriteAt,
                0,
                "정상",
                $"마지막 DB 저장 완료 {lastSuccessfulWriteAt:HH:mm:ss}");
        }

        if (age <= errorThreshold)
        {
            return new StorageStatusSnapshot(
                StorageHealth.Delayed,
                lastSuccessfulWriteAt,
                0,
                "지연",
                $"최근 저장은 {lastSuccessfulWriteAt:HH:mm:ss}, 새 저장 대기 중");
        }

        return new StorageStatusSnapshot(
            StorageHealth.Error,
            lastSuccessfulWriteAt,
            0,
            "정지",
            $"마지막 DB 저장 {lastSuccessfulWriteAt:HH:mm:ss}");
    }

    private static DateTimeOffset ParseSqliteUtcTimestamp(string value)
    {
        var parsed = DateTime.Parse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        return new DateTimeOffset(parsed);
    }
}
