using EnvironmentalMonitoring.Domain;
using EnvironmentalMonitoring.Infrastructure;
using System.Diagnostics;

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

        var interval = TimeSpan.FromSeconds((int)blueprint.DefaultSamplingMode);
        var nextScheduledAt = DateTimeOffset.Now;

        while (!stoppingToken.IsCancellationRequested)
        {
            var cycleStopwatch = Stopwatch.StartNew();
            var sampledAt = DateTimeOffset.Now;
            var snapshot = await acquisitionGateway.CaptureAsync(sampledAt, stoppingToken);
            var storageStatus = await storageService.SaveSnapshotAsync(snapshot, stoppingToken);
            cycleStopwatch.Stop();

            logger.LogInformation(
                "Acquisition stored. Sampled at {Timestamp}. Cycle time {ElapsedMilliseconds} ms. Storage status: {Summary} ({Detail})",
                sampledAt,
                cycleStopwatch.ElapsedMilliseconds,
                storageStatus.Summary,
                storageStatus.Detail);

            nextScheduledAt += interval;
            var remaining = nextScheduledAt - DateTimeOffset.Now;

            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining, stoppingToken);
            }
            else
            {
                nextScheduledAt = DateTimeOffset.Now;
            }
        }
    }
}
