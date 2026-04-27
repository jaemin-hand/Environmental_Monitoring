namespace EnvironmentalMonitoring.Domain;

public interface IMonitoringAcquisitionGateway
{
    Task<AcquisitionSnapshot> CaptureAsync(
        MonitoringBlueprint blueprint,
        DateTimeOffset sampledAt,
        CancellationToken cancellationToken);
}
