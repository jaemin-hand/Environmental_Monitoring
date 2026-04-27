using EnvironmentalMonitoring.Domain;
using EnvironmentalMonitoring.Infrastructure;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace EnvironmentalMonitoring.Worker;

public sealed class Worker(
    ILogger<Worker> logger,
    IOptions<MonitoringRuntimeOptions> runtimeOptions,
    MonitoringSettingsStore settingsStore,
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

        var currentBlueprint = blueprint;
        var interval = GetSamplingInterval(currentBlueprint);
        var nextScheduledAt = DateTimeOffset.Now;

        while (!stoppingToken.IsCancellationRequested)
        {
            currentBlueprint = RefreshBlueprint(currentBlueprint);
            var refreshedInterval = GetSamplingInterval(currentBlueprint);
            if (refreshedInterval != interval)
            {
                logger.LogInformation(
                    "Sampling mode changed to {SamplingMode}. Next acquisition schedule has been reset.",
                    currentBlueprint.DefaultSamplingMode);

                interval = refreshedInterval;
                nextScheduledAt = DateTimeOffset.Now;
            }

            var cycleStopwatch = Stopwatch.StartNew();
            var sampledAt = DateTimeOffset.Now;
            var snapshot = await acquisitionGateway.CaptureAsync(currentBlueprint, sampledAt, stoppingToken);
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
                (currentBlueprint, interval, nextScheduledAt) = await DelayUntilNextCycleAsync(
                    currentBlueprint,
                    interval,
                    nextScheduledAt,
                    stoppingToken);
            }
            else
            {
                nextScheduledAt = DateTimeOffset.Now;
            }
        }
    }

    private async Task<(MonitoringBlueprint Blueprint, TimeSpan Interval, DateTimeOffset NextScheduledAt)> DelayUntilNextCycleAsync(
        MonitoringBlueprint currentBlueprint,
        TimeSpan interval,
        DateTimeOffset nextScheduledAt,
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var remaining = nextScheduledAt - DateTimeOffset.Now;
            if (remaining <= TimeSpan.Zero)
            {
                return (currentBlueprint, interval, nextScheduledAt);
            }

            await Task.Delay(remaining < TimeSpan.FromSeconds(1) ? remaining : TimeSpan.FromSeconds(1), stoppingToken);

            var refreshedBlueprint = RefreshBlueprint(currentBlueprint);
            var refreshedInterval = GetSamplingInterval(refreshedBlueprint);
            if (refreshedInterval == interval)
            {
                currentBlueprint = refreshedBlueprint;
                continue;
            }

            logger.LogInformation(
                "Sampling mode changed to {SamplingMode}. Pending delay has been interrupted.",
                refreshedBlueprint.DefaultSamplingMode);

            return (refreshedBlueprint, refreshedInterval, DateTimeOffset.Now);
        }

        return (currentBlueprint, interval, nextScheduledAt);
    }

    private MonitoringBlueprint RefreshBlueprint(MonitoringBlueprint fallbackBlueprint)
    {
        try
        {
            var settingsDocument = settingsStore.LoadOrCreateDefaults(
                runtimeOptions.Value,
                fallbackBlueprint);
            var resolvedOptions = MonitoringRuntimeOptionsResolver.Resolve(
                runtimeOptions.Value,
                settingsDocument);

            return MonitoringBlueprintComposer.Compose(resolvedOptions, settingsDocument);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to reload runtime settings. Continuing with previous monitoring blueprint.");

            return fallbackBlueprint;
        }
    }

    private static TimeSpan GetSamplingInterval(MonitoringBlueprint blueprint) =>
        TimeSpan.FromSeconds((int)blueprint.DefaultSamplingMode);
}
