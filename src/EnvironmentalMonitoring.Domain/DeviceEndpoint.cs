namespace EnvironmentalMonitoring.Domain;

public sealed record DeviceEndpoint(
    string Key,
    string DisplayName,
    string Protocol,
    string IpAddress,
    int Port,
    string Notes);
