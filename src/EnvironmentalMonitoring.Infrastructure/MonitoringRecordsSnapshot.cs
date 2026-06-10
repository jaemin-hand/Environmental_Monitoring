using EnvironmentalMonitoring.Domain;

namespace EnvironmentalMonitoring.Infrastructure;

public sealed record MonitoringSampleRecord(
    DateTimeOffset SampledAt,
    string ChannelCode,
    ChannelKind Kind,
    string Unit,
    double? RawValue,
    double? CorrectedValue,
    SampleQualityStatus QualityStatus);

public sealed record MonitoringAlarmRecord(
    long Id,
    DateTimeOffset OccurredAt,
    DateTimeOffset? AcknowledgedAt,
    DateTimeOffset? ResolvedAt,
    string ChannelCode,
    string AlarmType,
    MonitoringEventSeverity Severity,
    double? MeasuredValue,
    double? ThresholdValue,
    double? TriggerValue,
    double? CurrentValue,
    double? WorstValue,
    DateTimeOffset? WorstAt,
    DateTimeOffset? ReturnedAt,
    double? ReturnValue,
    string Status,
    string Message);
