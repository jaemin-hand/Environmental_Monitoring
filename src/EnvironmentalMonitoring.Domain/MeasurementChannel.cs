namespace EnvironmentalMonitoring.Domain;

public sealed record MeasurementChannel(
    int ChannelNumber,
    string Name,
    ChannelKind Kind,
    string Unit,
    string DeviceKey,
    int DeviceChannelIndex,
    bool SupportsDeviationAlarm,
    decimal? DefaultDeviationThreshold,
    decimal? TargetValue,
    decimal CalibrationOffset);
