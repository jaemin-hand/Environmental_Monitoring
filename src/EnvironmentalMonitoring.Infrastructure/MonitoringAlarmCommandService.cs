using Microsoft.Data.Sqlite;

namespace EnvironmentalMonitoring.Infrastructure;

public sealed class MonitoringAlarmCommandService(MonitoringStorageLayout storageLayout)
{
    private readonly string _connectionString = $"Data Source={storageLayout.DatabaseFilePath}";

    public async Task<bool> AcknowledgeAlarmAsync(
        long alarmId,
        DateTimeOffset acknowledgedAt,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(storageLayout.DatabaseFilePath))
        {
            return false;
        }

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureEventLogTableAsync(connection, cancellationToken);
        await EnsureAlarmActionColumnsAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE alarm_events
            SET acknowledged_at = @AcknowledgedAt
            WHERE id = @AlarmId
              AND resolved_at IS NULL
              AND acknowledged_at IS NULL;
            """;
        command.Parameters.AddWithValue("@AcknowledgedAt", acknowledgedAt.ToString("O"));
        command.Parameters.AddWithValue("@AlarmId", alarmId);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0;
    }

    public async Task<int> AcknowledgeActiveAlarmsAsync(
        DateTimeOffset acknowledgedAt,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(storageLayout.DatabaseFilePath))
        {
            return 0;
        }

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureEventLogTableAsync(connection, cancellationToken);
        await EnsureAlarmActionColumnsAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE alarm_events
            SET acknowledged_at = @AcknowledgedAt
            WHERE resolved_at IS NULL
              AND acknowledged_at IS NULL;
            """;
        command.Parameters.AddWithValue("@AcknowledgedAt", acknowledgedAt.ToString("O"));

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> ResolveActiveAlarmsWithActionAsync(
        DateTimeOffset handledAt,
        string handledBy,
        string actionNote,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(storageLayout.DatabaseFilePath))
        {
            return 0;
        }

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureEventLogTableAsync(connection, cancellationToken);
        await EnsureAlarmActionColumnsAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE alarm_events
            SET acknowledged_at = COALESCE(acknowledged_at, @HandledAt),
                acknowledged_by = @HandledBy,
                action_note = @ActionNote,
                resolved_at = COALESCE(resolved_at, @HandledAt)
            WHERE resolved_at IS NULL;
            """;
        command.Parameters.AddWithValue("@HandledAt", handledAt.ToString("O"));
        command.Parameters.AddWithValue("@HandledBy", handledBy);
        command.Parameters.AddWithValue("@ActionNote", actionNote);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected > 0)
        {
            await InsertResolvedAlarmLogsAsync(
                connection,
                handledAt,
                handledBy,
                actionNote,
                cancellationToken);
        }

        return affected;
    }

    public async Task<int> ResolveAlarmWithActionAsync(
        long alarmId,
        DateTimeOffset handledAt,
        string handledBy,
        string actionNote,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(storageLayout.DatabaseFilePath))
        {
            return 0;
        }

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureEventLogTableAsync(connection, cancellationToken);
        await EnsureAlarmActionColumnsAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE alarm_events
            SET acknowledged_at = COALESCE(acknowledged_at, @HandledAt),
                acknowledged_by = @HandledBy,
                action_note = @ActionNote,
                resolved_at = COALESCE(resolved_at, @HandledAt)
            WHERE id = @AlarmId
              AND resolved_at IS NULL;
            """;
        command.Parameters.AddWithValue("@AlarmId", alarmId);
        command.Parameters.AddWithValue("@HandledAt", handledAt.ToString("O"));
        command.Parameters.AddWithValue("@HandledBy", handledBy);
        command.Parameters.AddWithValue("@ActionNote", actionNote);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected > 0)
        {
            await InsertResolvedAlarmLogAsync(
                connection,
                alarmId,
                handledAt,
                handledBy,
                actionNote,
                cancellationToken);
        }

        return affected;
    }

    private static async Task EnsureEventLogTableAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await ExecuteNonQueryAsync(connection, MonitoringDatabaseSchema.Sql, cancellationToken);
    }

    private static async Task InsertResolvedAlarmLogsAsync(
        SqliteConnection connection,
        DateTimeOffset handledAt,
        string handledBy,
        string actionNote,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO event_logs (
                event_type,
                channel_id,
                alarm_id,
                severity,
                value,
                message,
                occurred_at
            )
            SELECT
                'ALARM_RESOLVED',
                a.channel_id,
                a.id,
                'Info',
                a.measured_value,
                c.name || ' 알람 조치 완료 / ' || @HandledBy ||
                    CASE WHEN @ActionNote = '' THEN '' ELSE ' / ' || @ActionNote END,
                @HandledAt
            FROM alarm_events a
            JOIN channels c ON c.id = a.channel_id
            WHERE a.resolved_at = @HandledAt
              AND a.acknowledged_by = @HandledBy
              AND a.action_note = @ActionNote;
            """;
        command.Parameters.AddWithValue("@HandledAt", handledAt.ToString("O"));
        command.Parameters.AddWithValue("@HandledBy", handledBy);
        command.Parameters.AddWithValue("@ActionNote", actionNote);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertResolvedAlarmLogAsync(
        SqliteConnection connection,
        long alarmId,
        DateTimeOffset handledAt,
        string handledBy,
        string actionNote,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO event_logs (
                event_type,
                channel_id,
                alarm_id,
                severity,
                value,
                message,
                occurred_at
            )
            SELECT
                'ALARM_RESOLVED',
                a.channel_id,
                a.id,
                'Info',
                a.measured_value,
                c.name || ' 알람 조치 완료 / ' || @HandledBy ||
                    CASE WHEN @ActionNote = '' THEN '' ELSE ' / ' || @ActionNote END,
                @HandledAt
            FROM alarm_events a
            JOIN channels c ON c.id = a.channel_id
            WHERE a.id = @AlarmId;
            """;
        command.Parameters.AddWithValue("@AlarmId", alarmId);
        command.Parameters.AddWithValue("@HandledAt", handledAt.ToString("O"));
        command.Parameters.AddWithValue("@HandledBy", handledBy);
        command.Parameters.AddWithValue("@ActionNote", actionNote);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureAlarmActionColumnsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA table_info(alarm_events);";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                columns.Add(reader.GetString(1));
            }
        }

        if (!columns.Contains("acknowledged_by"))
        {
            await ExecuteNonQueryAsync(
                connection,
                "ALTER TABLE alarm_events ADD COLUMN acknowledged_by TEXT;",
                cancellationToken);
        }

        if (!columns.Contains("action_note"))
        {
            await ExecuteNonQueryAsync(
                connection,
                "ALTER TABLE alarm_events ADD COLUMN action_note TEXT;",
                cancellationToken);
        }
    }

    private static async Task ExecuteNonQueryAsync(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
