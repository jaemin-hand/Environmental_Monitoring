using EnvironmentalMonitoring.Domain;

namespace EnvironmentalMonitoring.Infrastructure;

public sealed class PlaceholderMonitoringAcquisitionGateway(
    MonitoringBlueprint blueprint) : IMonitoringAcquisitionGateway
{
    public Task<AcquisitionSnapshot> CaptureAsync(
        DateTimeOffset sampledAt,
        CancellationToken cancellationToken)
    {
        var second = sampledAt.Second;
        var measurements = blueprint.Channels
            .Select(channel =>
            {
                var rawValue = channel.Kind switch
                {
                    ChannelKind.Temperature => 23.0 + (channel.ChannelNumber % 4) + Math.Sin(second / 10.0 + channel.ChannelNumber) * 0.4,
                    ChannelKind.Humidity => 45.0 + Math.Sin(second / 15.0) * 2.0,
                    ChannelKind.Pressure => 101.3 + Math.Cos(second / 12.0) * 0.15,
                    _ => 0.0,
                };

                return new CapturedMeasurement(
                    channel,
                    RawValue: Math.Round(rawValue, 3),
                    CorrectedValue: Math.Round(rawValue, 3),
                    QualityStatus: SampleQualityStatus.Normal);
            })
            .ToList();

        AcquisitionSnapshot snapshot = new(
            sampledAt,
            blueprint.DefaultSamplingMode,
            measurements,
            AcquisitionBatchStatus.Success);

        return Task.FromResult(snapshot);
    }
}
