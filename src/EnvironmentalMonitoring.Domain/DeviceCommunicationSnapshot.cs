namespace EnvironmentalMonitoring.Domain;

public sealed record DeviceCommunicationSnapshot(
    string DeviceKey,
    string DeviceName,
    double? ResponseMilliseconds,
    bool IsHealthy,
    DateTimeOffset MeasuredAt);
