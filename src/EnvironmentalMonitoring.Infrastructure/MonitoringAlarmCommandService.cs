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
}
