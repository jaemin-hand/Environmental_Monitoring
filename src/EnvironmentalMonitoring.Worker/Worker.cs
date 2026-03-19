using EnvironmentalMonitoring.Domain;
using EnvironmentalMonitoring.Infrastructure;

namespace EnvironmentalMonitoring.Worker;

public sealed class Worker(
    ILogger<Worker> logger,
    MonitoringBlueprint blueprint,
    MonitoringStorageLayout storageLayout) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        storageLayout.EnsureCreated();

        logger.LogInformation(
            "Monitoring worker started with {DeviceCount} devices and {ChannelCount} channels. Default mode: {SamplingMode}",
            blueprint.Devices.Count,
            blueprint.Channels.Count,
            blueprint.DefaultSamplingMode);

        logger.LogInformation("Runtime database path: {DatabasePath}", storageLayout.DatabaseFilePath);

        var delay = TimeSpan.FromSeconds((int)blueprint.DefaultSamplingMode);

        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation(
                "Acquisition placeholder cycle at {Timestamp}. Next step: Modbus TCP polling and SQLite persistence.",
                DateTimeOffset.Now);

            await Task.Delay(delay, stoppingToken);
        }
    }
}
