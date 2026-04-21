namespace EnvironmentalMonitoring.Domain;

public sealed record MeasurementChannel(
    int ChannelNumber,
    string Name,
    string DisplayName,
    string LocationName,
    ChannelKind Kind,
    string Unit,
    string DeviceKey,
    int DeviceChannelIndex,
    bool SupportsDeviationAlarm,
    decimal? DefaultDeviationThreshold,
    decimal? TargetValue,
    decimal? LowAlarmLimit,
    decimal? HighAlarmLimit,
    decimal CalibrationScale,
    decimal CalibrationOffset);
