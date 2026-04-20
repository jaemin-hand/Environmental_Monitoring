using EnvironmentalMonitoring.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EnvironmentalMonitoring.Infrastructure;

public sealed class ModbusTcpMonitoringAcquisitionGateway(
    MonitoringBlueprint blueprint,
    IOptions<MonitoringRuntimeOptions> options,
    ModbusTcpClient modbusTcpClient,
    ILogger<ModbusTcpMonitoringAcquisitionGateway> logger) : IMonitoringAcquisitionGateway
{
    public async Task<AcquisitionSnapshot> CaptureAsync(
        DateTimeOffset sampledAt,
        CancellationToken cancellationToken)
    {
        var measurements = new List<CapturedMeasurement>(blueprint.Channels.Count);
        var successfulReads = 0;

        foreach (var channel in blueprint.Channels)
        {
            try
            {
                var map = GetRegisterMap(channel);
                var device = GetDevice(channel.DeviceKey);
                var registers = await modbusTcpClient.ReadHoldingRegistersAsync(
                    device.IpAddress,
                    device.Port,
                    map.UnitId,
                    map.Address,
                    map.RegisterCount,
                    cancellationToken);

                var rawValue = ModbusRegisterDecoder.Decode(registers, map);
                var correctedValue = rawValue + (double)channel.CalibrationOffset;

                measurements.Add(new CapturedMeasurement(
                    channel,
                    RawValue: Math.Round(rawValue, 3),
                    CorrectedValue: Math.Round(correctedValue, 3),
                    QualityStatus: SampleQualityStatus.Normal));

                successfulReads++;
            }
            catch (Exception ex)
            {
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
            batchStatus);
    }

    private DeviceEndpoint GetDevice(string deviceKey)
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
}
