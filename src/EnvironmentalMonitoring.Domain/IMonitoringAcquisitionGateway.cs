namespace EnvironmentalMonitoring.Domain;

public interface IMonitoringAcquisitionGateway
{
    Task<AcquisitionSnapshot> CaptureAsync(
        DateTimeOffset sampledAt,
        CancellationToken cancellationToken);
}
