using EnvironmentalMonitoring.Domain;
using EnvironmentalMonitoring.Infrastructure;

namespace EnvironmentalMonitoring.Worker;

public sealed class Worker(
    ILogger<Worker> logger,
    MonitoringBlueprint blueprint,
    MonitoringStorageLayout storageLayout,
    SqliteMonitoringStorageService storageService,
    IMonitoringAcquisitionGateway acquisitionGateway) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await storageService.InitializeAsync(stoppingToken);

        logger.LogInformation(
            "Monitoring worker started with {DeviceCount} devices and {ChannelCount} channels. Default mode: {SamplingMode}",
            blueprint.Devices.Count,
            blueprint.Channels.Count,
            blueprint.DefaultSamplingMode);

        logger.LogInformation("Runtime database path: {DatabasePath}", storageLayout.DatabaseFilePath);

        var delay = TimeSpan.FromSeconds((int)blueprint.DefaultSamplingMode);

        while (!stoppingToken.IsCancellationRequested)
        {
            var sampledAt = DateTimeOffset.Now;
            var snapshot = await acquisitionGateway.CaptureAsync(sampledAt, stoppingToken);
            var storageStatus = await storageService.SaveSnapshotAsync(snapshot, stoppingToken);

            logger.LogInformation(
                "Acquisition stored at {Timestamp}. Storage status: {Summary} ({Detail})",
                sampledAt,
                storageStatus.Summary,
                storageStatus.Detail);

            await Task.Delay(delay, stoppingToken);
        }
    }
}
