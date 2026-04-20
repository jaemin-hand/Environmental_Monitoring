using EnvironmentalMonitoring.Domain;
using Microsoft.Data.Sqlite;

namespace EnvironmentalMonitoring.Infrastructure;

public sealed class SqliteMonitoringStorageService(
    MonitoringStorageLayout storageLayout,
    MonitoringBlueprint blueprint)
{
    private readonly string _connectionString = $"Data Source={storageLayout.DatabaseFilePath}";
    private DateTimeOffset? _lastSuccessfulWriteAt;
    private string? _lastFailureMessage;
    private int _pendingWriteCount;

    public string DatabaseFilePath => storageLayout.DatabaseFilePath;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        storageLayout.EnsureCreated();

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await ApplyPragmasAsync(connection, cancellationToken);
        await ExecuteNonQueryAsync(connection, MonitoringDatabaseSchema.Sql, cancellationToken);
        await SeedBlueprintAsync(connection, cancellationToken);
    }

    public async Task<StorageStatusSnapshot> SaveSnapshotAsync(
        AcquisitionSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _pendingWriteCount);

        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await ApplyPragmasAsync(connection, cancellationToken);
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

            var batchId = await InsertBatchAsync(connection, transaction, snapshot, cancellationToken);

            foreach (var measurement in snapshot.Measurements)
            {
                await InsertSampleAsync(connection, transaction, batchId, measurement, cancellationToken);
            }

            await SynchronizeAlarmEventsAsync(
                connection,
                transaction,
                batchId,
                snapshot,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            _lastSuccessfulWriteAt = snapshot.SampledAt;
            _lastFailureMessage = null;
        }
        catch (Exception ex)
        {
            _lastFailureMessage = ex.Message;
        }
        finally
        {
            Interlocked.Decrement(ref _pendingWriteCount);
        }

        return GetCurrentStatus(snapshot.SamplingMode, DateTimeOffset.Now);
    }

    public StorageStatusSnapshot GetCurrentStatus(
        SamplingMode samplingMode,
        DateTimeOffset now)
    {
        var pendingWrites = Volatile.Read(ref _pendingWriteCount);
        var healthyThreshold = TimeSpan.FromSeconds((int)samplingMode * 2);
        var errorThreshold = TimeSpan.FromSeconds((int)samplingMode * 5);

        if (_lastFailureMessage is not null)
        {
            return new StorageStatusSnapshot(
                StorageHealth.Error,
                _lastSuccessfulWriteAt,
                pendingWrites,
                "Storage error",
                _lastFailureMessage);
        }

        if (_lastSuccessfulWriteAt is null)
        {
            return new StorageStatusSnapshot(
                StorageHealth.Delayed,
                null,
                pendingWrites,
                "Storage ready",
                "No samples have been written yet.");
        }

        var age = now - _lastSuccessfulWriteAt.Value;

        if (age <= healthyThreshold && pendingWrites == 0)
        {
            return new StorageStatusSnapshot(
                StorageHealth.Healthy,
                _lastSuccessfulWriteAt,
                pendingWrites,
                "Storage healthy",
                $"Last write succeeded at {_lastSuccessfulWriteAt.Value:HH:mm:ss}");
        }

        if (age <= errorThreshold)
        {
            return new StorageStatusSnapshot(
                StorageHealth.Delayed,
                _lastSuccessfulWriteAt,
                pendingWrites,
                "Storage delayed",
                $"Pending writes: {pendingWrites}, last success {_lastSuccessfulWriteAt.Value:HH:mm:ss}");
        }

        return new StorageStatusSnapshot(
            StorageHealth.Error,
            _lastSuccessfulWriteAt,
            pendingWrites,
            "Storage stalled",
            $"Last successful write {_lastSuccessfulWriteAt.Value:HH:mm:ss}");
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task ApplyPragmasAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        const string sql = """
            PRAGMA journal_mode=DELETE;
            PRAGMA synchronous=EXTRA;
            PRAGMA foreign_keys=ON;
            PRAGMA temp_store=MEMORY;
            PRAGMA busy_timeout=5000;
            """;

        await ExecuteNonQueryAsync(connection, sql, cancellationToken);
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

    private async Task SeedBlueprintAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        foreach (var device in blueprint.Devices)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO devices (code, name, protocol, ip_address, port, is_active)
                VALUES (@Code, @Name, @Protocol, @IpAddress, @Port, 1)
                ON CONFLICT(code) DO UPDATE SET
                    name = excluded.name,
                    protocol = excluded.protocol,
                    ip_address = excluded.ip_address,
                    port = excluded.port,
                    is_active = excluded.is_active;
                """;
            command.Parameters.AddWithValue("@Code", device.Key);
            command.Parameters.AddWithValue("@Name", device.DisplayName);
            command.Parameters.AddWithValue("@Protocol", device.Protocol);
            command.Parameters.AddWithValue("@IpAddress", device.IpAddress);
            command.Parameters.AddWithValue("@Port", device.Port);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var channel in blueprint.Channels)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO channels (code, name, kind, unit, device_id, device_channel_no, location_name, is_active)
                VALUES (
                    @Code,
                    @Name,
                    @Kind,
                    @Unit,
                    (SELECT id FROM devices WHERE code = @DeviceCode),
                    @DeviceChannelNo,
                    @LocationName,
                    1
                )
                ON CONFLICT(code) DO UPDATE SET
                    name = excluded.name,
                    kind = excluded.kind,
                    unit = excluded.unit,
                    device_id = excluded.device_id,
                    device_channel_no = excluded.device_channel_no,
                    location_name = excluded.location_name,
                    is_active = excluded.is_active;
                """;
            command.Parameters.AddWithValue("@Code", channel.Name);
            command.Parameters.AddWithValue("@Name", channel.Name);
            command.Parameters.AddWithValue("@Kind", channel.Kind.ToString());
            command.Parameters.AddWithValue("@Unit", channel.Unit);
            command.Parameters.AddWithValue("@DeviceCode", channel.DeviceKey);
            command.Parameters.AddWithValue("@DeviceChannelNo", channel.DeviceChannelIndex);
            command.Parameters.AddWithValue("@LocationName", channel.Name);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await UpsertSettingAsync(connection, transaction, "storage_journal_mode", "DELETE", cancellationToken);
        await UpsertSettingAsync(connection, transaction, "storage_synchronous", "EXTRA", cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task UpsertSettingAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string key,
        string value,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO app_settings (key, value, updated_at)
            VALUES (@Key, @Value, CURRENT_TIMESTAMP)
            ON CONFLICT(key) DO UPDATE SET
                value = excluded.value,
                updated_at = excluded.updated_at;
            """;
        command.Parameters.AddWithValue("@Key", key);
        command.Parameters.AddWithValue("@Value", value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<long> InsertBatchAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        AcquisitionSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO acquisition_batches (sampled_at, sampling_mode, status)
            VALUES (@SampledAt, @SamplingMode, @Status);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("@SampledAt", snapshot.SampledAt.ToString("O"));
        command.Parameters.AddWithValue("@SamplingMode", snapshot.SamplingMode.ToString());
        command.Parameters.AddWithValue("@Status", snapshot.Status.ToString());

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result);
    }

    private static async Task InsertSampleAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long batchId,
        CapturedMeasurement measurement,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO samples (batch_id, channel_id, raw_value, corrected_value, quality_status)
            VALUES (
                @BatchId,
                (SELECT id FROM channels WHERE code = @ChannelCode),
                @RawValue,
                @CorrectedValue,
                @QualityStatus
            );
            """;
        command.Parameters.AddWithValue("@BatchId", batchId);
        command.Parameters.AddWithValue("@ChannelCode", measurement.Channel.Name);
        command.Parameters.AddWithValue("@RawValue", measurement.RawValue);
        command.Parameters.AddWithValue("@CorrectedValue", measurement.CorrectedValue);
        command.Parameters.AddWithValue("@QualityStatus", measurement.QualityStatus.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task SynchronizeAlarmEventsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long batchId,
        AcquisitionSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var activeAlarms = EvaluateAlarms(snapshot)
            .ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);
        var openAlarms = await GetOpenAlarmsAsync(connection, transaction, cancellationToken);

        foreach (var activeAlarm in activeAlarms.Values)
        {
            if (!openAlarms.ContainsKey(activeAlarm.Key))
            {
                await InsertAlarmEventAsync(connection, transaction, batchId, snapshot.SampledAt, activeAlarm, cancellationToken);
            }
        }

        foreach (var openAlarm in openAlarms.Values)
        {
            if (!activeAlarms.ContainsKey(openAlarm.Key))
            {
                await ResolveAlarmEventAsync(connection, transaction, openAlarm.Id, snapshot.SampledAt, cancellationToken);
            }
        }
    }

    private static IReadOnlyList<AlarmState> EvaluateAlarms(AcquisitionSnapshot snapshot)
    {
        var alarms = new List<AlarmState>();

        foreach (var measurement in snapshot.Measurements)
        {
            var channel = measurement.Channel;

            if (measurement.QualityStatus == SampleQualityStatus.CommunicationError)
            {
                alarms.Add(new AlarmState(
                    BuildAlarmKey(channel.Name, "COMMUNICATION"),
                    channel.Name,
                    "COMMUNICATION",
                    "Warning",
                    double.NaN,
                    $"{BuildChannelLabel(channel)} 통신 이상"));
                continue;
            }

            if (double.IsNaN(measurement.CorrectedValue) || double.IsInfinity(measurement.CorrectedValue))
            {
                continue;
            }

            var value = measurement.CorrectedValue;

            if (IsOutOfPhysicalRange(channel.Kind, value))
            {
                alarms.Add(new AlarmState(
                    BuildAlarmKey(channel.Name, "OUT_OF_RANGE"),
                    channel.Name,
                    "OUT_OF_RANGE",
                    "Critical",
                    value,
                    $"{BuildChannelLabel(channel)} 범위 이탈 ({value:0.0}{channel.Unit})"));
                continue;
            }

            if (channel.SupportsDeviationAlarm
                && channel.TargetValue.HasValue
                && channel.DefaultDeviationThreshold.HasValue
                && Math.Abs(value - (double)channel.TargetValue.Value) > (double)channel.DefaultDeviationThreshold.Value)
            {
                alarms.Add(new AlarmState(
                    BuildAlarmKey(channel.Name, "DEVIATION"),
                    channel.Name,
                    "DEVIATION",
                    "Warning",
                    value,
                    $"{BuildChannelLabel(channel)} 설정값 이탈 (기준 {channel.TargetValue:0.#}{channel.Unit}, 측정 {value:0.0}{channel.Unit})"));
            }
        }

        return alarms;
    }

    private static bool IsOutOfPhysicalRange(ChannelKind kind, double value) => kind switch
    {
        ChannelKind.Temperature => value < -20.0 || value > 60.0,
        ChannelKind.Humidity => value < 0.0 || value > 100.0,
        ChannelKind.Pressure => value < 80.0 || value > 120.0,
        _ => false,
    };

    private static string BuildChannelLabel(MeasurementChannel channel) => channel.Kind switch
    {
        ChannelKind.Temperature => $"CH{channel.ChannelNumber} 온도",
        ChannelKind.Humidity => "H1 습도",
        ChannelKind.Pressure => "P1 압력",
        _ => channel.Name,
    };

    private static string BuildAlarmKey(string channelCode, string alarmType) => $"{channelCode}|{alarmType}";

    private static async Task<Dictionary<string, OpenAlarmState>> GetOpenAlarmsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        var alarms = new Dictionary<string, OpenAlarmState>(StringComparer.OrdinalIgnoreCase);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT a.id, c.code, a.alarm_type
            FROM alarm_events a
            JOIN channels c ON c.id = a.channel_id
            WHERE a.resolved_at IS NULL;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetInt64(0);
            var channelCode = reader.GetString(1);
            var alarmType = reader.GetString(2);
            var key = BuildAlarmKey(channelCode, alarmType);

            alarms[key] = new OpenAlarmState(id, key);
        }

        return alarms;
    }

    private static async Task InsertAlarmEventAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long batchId,
        DateTimeOffset occurredAt,
        AlarmState alarm,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO alarm_events (
                channel_id,
                batch_id,
                alarm_type,
                severity,
                measured_value,
                message,
                occurred_at
            )
            VALUES (
                (SELECT id FROM channels WHERE code = @ChannelCode),
                @BatchId,
                @AlarmType,
                @Severity,
                @MeasuredValue,
                @Message,
                @OccurredAt
            );
            """;
        command.Parameters.AddWithValue("@ChannelCode", alarm.ChannelCode);
        command.Parameters.AddWithValue("@BatchId", batchId);
        command.Parameters.AddWithValue("@AlarmType", alarm.AlarmType);
        command.Parameters.AddWithValue("@Severity", alarm.Severity);
        command.Parameters.AddWithValue("@MeasuredValue", double.IsNaN(alarm.MeasuredValue) ? DBNull.Value : alarm.MeasuredValue);
        command.Parameters.AddWithValue("@Message", alarm.Message);
        command.Parameters.AddWithValue("@OccurredAt", occurredAt.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ResolveAlarmEventAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long alarmId,
        DateTimeOffset resolvedAt,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE alarm_events
            SET resolved_at = @ResolvedAt
            WHERE id = @AlarmId
              AND resolved_at IS NULL;
            """;
        command.Parameters.AddWithValue("@ResolvedAt", resolvedAt.ToString("O"));
        command.Parameters.AddWithValue("@AlarmId", alarmId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private sealed record AlarmState(
        string Key,
        string ChannelCode,
        string AlarmType,
        string Severity,
        double MeasuredValue,
        string Message);

    private sealed record OpenAlarmState(
        long Id,
        string Key);
}
