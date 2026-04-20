using EnvironmentalMonitoring.Domain;

namespace EnvironmentalMonitoring.Infrastructure;

public sealed record MonitoringDashboardSnapshot(
    StorageStatusSnapshot StorageStatus,
    int ActiveAlarmCount,
    MonitoringEventSeverity HighestActiveAlarmSeverity,
    IReadOnlyList<MonitoringChannelSnapshot> ChannelSnapshots,
    IReadOnlyList<MonitoringTrendPoint> TrendPoints,
    IReadOnlyList<MonitoringEventSnapshot> RecentEvents);

public sealed record MonitoringChannelSnapshot(
    string ChannelCode,
    ChannelKind Kind,
    string Unit,
    double? Value,
    SampleQualityStatus QualityStatus,
    DateTimeOffset? SampledAt);

public sealed record MonitoringTrendPoint(
    DateTimeOffset SampledAt,
    double AverageTemperature,
    double? Humidity);

public sealed record MonitoringEventSnapshot(
    DateTimeOffset OccurredAt,
    string Message,
    MonitoringEventSeverity Severity);

public enum MonitoringEventSeverity
{
    Info,
    Warning,
    Critical,
}
