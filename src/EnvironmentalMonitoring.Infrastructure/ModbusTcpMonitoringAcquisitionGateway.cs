using EnvironmentalMonitoring.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace EnvironmentalMonitoring.Infrastructure;

public sealed class ModbusTcpMonitoringAcquisitionGateway(
    IOptions<MonitoringRuntimeOptions> options,
    ModbusTcpClient modbusTcpClient,
    ILogger<ModbusTcpMonitoringAcquisitionGateway> logger) : IMonitoringAcquisitionGateway
{
    public async Task<AcquisitionSnapshot> CaptureAsync(
        MonitoringBlueprint blueprint,
        DateTimeOffset sampledAt,
        CancellationToken cancellationToken)
    {
        var measurements = new List<CapturedMeasurement>(blueprint.Channels.Count);
        var deviceCommunication = new Dictionary<string, DeviceCommunicationAccumulator>(StringComparer.OrdinalIgnoreCase);
        var successfulReads = 0;

        foreach (var channel in blueprint.Channels.Where(channel => channel.IsActive))
        {
            var device = GetDevice(blueprint, channel.DeviceKey);
            var readStopwatch = Stopwatch.StartNew();

            try
            {
                var map = GetRegisterMap(channel);
                var registers = await modbusTcpClient.ReadHoldingRegistersAsync(
                    device.IpAddress,
                    device.Port,
                    map.UnitId,
                    map.Address,
                    map.RegisterCount,
                    cancellationToken);
                readStopwatch.Stop();
                RecordDeviceCommunication(
                    deviceCommunication,
                    device,
                    readStopwatch.Elapsed.TotalMilliseconds,
                    isHealthy: true,
                    sampledAt);

                var rawValue = ModbusRegisterDecoder.Decode(registers, map);
                var correctedValue = CalibrationCalculator.Apply(
                    rawValue,
                    channel.CalibrationScale,
                    channel.CalibrationOffset,
                    channel.CalibrationPoints);

                measurements.Add(new CapturedMeasurement(
                    channel,
                    RawValue: Math.Round(rawValue, 3),
                    CorrectedValue: Math.Round(correctedValue, 3),
                    QualityStatus: SampleQualityStatus.Normal));

                successfulReads++;
            }
            catch (Exception ex)
            {
                readStopwatch.Stop();
                RecordDeviceCommunication(
                    deviceCommunication,
                    device,
                    readStopwatch.Elapsed.TotalMilliseconds,
                    isHealthy: false,
                    sampledAt);

                logger.LogWarning(
                    ex,
                    "Failed to capture channel {ChannelCode} from device {DeviceKey}.",
                    channel.Name,
                    channel.DeviceKey);

                measurements.Add(new CapturedMeasurement(
                    channel,
                    RawValue: double.NaN,
                    CorrectedValue: double.NaN,
                    QualityStatus: SampleQualityStatus.CommunicationError));
            }
        }

        var batchStatus = successfulReads switch
        {
            0 => AcquisitionBatchStatus.Failed,
            var count when count == blueprint.Channels.Count => AcquisitionBatchStatus.Success,
            _ => AcquisitionBatchStatus.Partial,
        };

        return new AcquisitionSnapshot(
            sampledAt,
            blueprint.DefaultSamplingMode,
            measurements,
            batchStatus,
            deviceCommunication.Values
                .Select(item => item.ToSnapshot())
                .ToArray());
    }

    private static void RecordDeviceCommunication(
        Dictionary<string, DeviceCommunicationAccumulator> deviceCommunication,
        DeviceEndpoint device,
        double responseMilliseconds,
        bool isHealthy,
        DateTimeOffset measuredAt)
    {
        if (!deviceCommunication.TryGetValue(device.Key, out var accumulator))
        {
            accumulator = new DeviceCommunicationAccumulator(device.Key, device.DisplayName);
            deviceCommunication[device.Key] = accumulator;
        }

        accumulator.Record(responseMilliseconds, isHealthy, measuredAt);
    }

    private static DeviceEndpoint GetDevice(MonitoringBlueprint blueprint, string deviceKey)
    {
        var device = blueprint.Devices.FirstOrDefault(item => item.Key == deviceKey);

        return device
            ?? throw new InvalidOperationException($"Device '{deviceKey}' is not defined in the monitoring blueprint.");
    }

    private ModbusChannelMapOptions GetRegisterMap(MeasurementChannel channel)
    {
        if (options.Value.RegisterMaps.TryGetValue(channel.Name, out var map))
        {
            return map;
        }

        throw new InvalidOperationException(
            $"Register map for channel '{channel.Name}' is not configured. Add Monitoring:RegisterMaps:{channel.Name} settings.");
    }

    private sealed class DeviceCommunicationAccumulator(string deviceKey, string deviceName)
    {
        private double? _maxResponseMilliseconds;
        private bool _isHealthy = true;
        private DateTimeOffset _measuredAt;

        public void Record(
            double responseMilliseconds,
            bool isHealthy,
            DateTimeOffset measuredAt)
        {
            _maxResponseMilliseconds = _maxResponseMilliseconds.HasValue
                ? Math.Max(_maxResponseMilliseconds.Value, responseMilliseconds)
                : responseMilliseconds;
            _isHealthy &= isHealthy;
            _measuredAt = measuredAt;
        }

        public DeviceCommunicationSnapshot ToSnapshot() =>
            new(
                deviceKey,
                deviceName,
                _maxResponseMilliseconds.HasValue ? Math.Round(_maxResponseMilliseconds.Value, 1) : null,
                _isHealthy,
                _measuredAt);
    }
}
