namespace EnvironmentalMonitoring.Infrastructure;

public sealed record CommunicationStatusSnapshot(
    DateTimeOffset UpdatedAt,
    double? MaxResponseMilliseconds,
    IReadOnlyList<DeviceCommunicationStatusItem> Devices);

public sealed record DeviceCommunicationStatusItem(
    string DeviceKey,
    string DeviceName,
    double? ResponseMilliseconds,
    bool IsHealthy,
    DateTimeOffset MeasuredAt);
