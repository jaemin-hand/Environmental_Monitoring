using EnvironmentalMonitoring.Domain;
using Microsoft.Extensions.Options;

namespace EnvironmentalMonitoring.Infrastructure;

public sealed class PlaceholderMonitoringAcquisitionGateway(
    IOptions<MonitoringRuntimeOptions> options) : IMonitoringAcquisitionGateway
{
    private readonly MonitoringRuntimeOptions _options = options.Value;

    public Task<AcquisitionSnapshot> CaptureAsync(
        MonitoringBlueprint blueprint,
        DateTimeOffset sampledAt,
        CancellationToken cancellationToken)
    {
        var measurements = blueprint.Channels
            .Where(channel => channel.IsActive)
            .Select(channel => CreateMeasurement(channel, sampledAt))
            .ToList();

        var batchStatus = measurements.Any(item => item.QualityStatus == SampleQualityStatus.CommunicationError)
            ? AcquisitionBatchStatus.Partial
            : AcquisitionBatchStatus.Success;

        AcquisitionSnapshot snapshot = new(
            sampledAt,
            blueprint.DefaultSamplingMode,
            measurements,
            batchStatus,
            CreateCommunicationSnapshots(blueprint, measurements, sampledAt));

        return Task.FromResult(snapshot);
    }

    private static IReadOnlyList<DeviceCommunicationSnapshot> CreateCommunicationSnapshots(
        MonitoringBlueprint blueprint,
        IReadOnlyList<CapturedMeasurement> measurements,
        DateTimeOffset sampledAt)
    {
        var measurementsByDevice = measurements
            .GroupBy(item => item.Channel.DeviceKey)
            .ToDictionary(
                item => item.Key,
                item => item.Any(measurement => measurement.QualityStatus == SampleQualityStatus.CommunicationError),
                StringComparer.OrdinalIgnoreCase);

        return blueprint.Devices
            .Where(device => measurementsByDevice.ContainsKey(device.Key))
            .Select(device =>
            {
                var hasCommunicationError = measurementsByDevice[device.Key];
                return new DeviceCommunicationSnapshot(
                    device.Key,
                    device.DisplayName,
                    hasCommunicationError ? 1500d : 132d,
                    !hasCommunicationError,
                    sampledAt);
            })
            .ToArray();
    }

    private CapturedMeasurement CreateMeasurement(
        MeasurementChannel channel,
        DateTimeOffset sampledAt)
    {
        var second = sampledAt.Second;
        var rawValue = channel.Kind switch
        {
            ChannelKind.Temperature => 23.0 + Math.Sin(second / 10.0 + channel.ChannelNumber) * 0.6,
            ChannelKind.Humidity => 45.0 + Math.Sin(second / 15.0) * 2.0,
            ChannelKind.Pressure => 101.3 + Math.Cos(second / 12.0) * 0.15,
            _ => 0.0,
        };

        var qualityStatus = SampleQualityStatus.Normal;
        var simulatedValue = rawValue;

        if (_options.PlaceholderProfile.Equals("AlarmDemo", StringComparison.OrdinalIgnoreCase))
        {
            ApplyAlarmDemoProfile(channel, sampledAt, ref simulatedValue, ref qualityStatus);
        }

        var correctedValue = CalibrationCalculator.Apply(
            simulatedValue,
            channel.CalibrationScale,
            channel.CalibrationOffset,
            channel.CalibrationPoints);

        return new CapturedMeasurement(
            channel,
            RawValue: qualityStatus == SampleQualityStatus.CommunicationError
                ? double.NaN
                : Math.Round(simulatedValue, 3),
            CorrectedValue: qualityStatus == SampleQualityStatus.CommunicationError
                ? double.NaN
                : Math.Round(correctedValue, 3),
            QualityStatus: qualityStatus);
    }

    private void ApplyAlarmDemoProfile(
        MeasurementChannel channel,
        DateTimeOffset sampledAt,
        ref double correctedValue,
        ref SampleQualityStatus qualityStatus)
    {
        var cycleSeconds = Math.Max(5, _options.PlaceholderCycleSeconds);
        var phase = (int)((sampledAt.ToUnixTimeSeconds() / cycleSeconds) % 4);

        switch (phase)
        {
            case 1:
                if (channel.Name == "T04")
                {
                    correctedValue = 31.2;
                }
                break;

            case 2:
                if (channel.Name == "T04")
                {
                    correctedValue = 31.8;
                }

                if (channel.Name == "P01")
                {
                    correctedValue = 121.4;
                }
                break;

            case 3:
                if (channel.Name == "H01")
                {
                    qualityStatus = SampleQualityStatus.CommunicationError;
                }

                if (channel.Name == "T02")
                {
                    correctedValue = 29.4;
                }
                break;
        }
    }
}
