namespace EnvironmentalMonitoring.Infrastructure;

public static class MonitoringRuntimeOptionsResolver
{
    public static MonitoringRuntimeOptions Resolve(
        MonitoringRuntimeOptions bootstrapOptions,
        RuntimeMonitoringSettingsDocument settingsDocument)
    {
        return new MonitoringRuntimeOptions
        {
            GatewayMode = string.IsNullOrWhiteSpace(settingsDocument.Monitoring.GatewayMode)
                ? bootstrapOptions.GatewayMode
                : settingsDocument.Monitoring.GatewayMode,
            DataRoot = string.IsNullOrWhiteSpace(settingsDocument.Monitoring.DataRoot)
                ? bootstrapOptions.DataRoot
                : settingsDocument.Monitoring.DataRoot,
            DefaultSamplingMode = settingsDocument.Monitoring.DefaultSamplingMode,
            PlaceholderProfile = string.IsNullOrWhiteSpace(settingsDocument.Monitoring.PlaceholderProfile)
                ? bootstrapOptions.PlaceholderProfile
                : settingsDocument.Monitoring.PlaceholderProfile,
            PlaceholderCycleSeconds = settingsDocument.Monitoring.PlaceholderCycleSeconds <= 0
                ? bootstrapOptions.PlaceholderCycleSeconds
                : settingsDocument.Monitoring.PlaceholderCycleSeconds,
            RegisterMaps = settingsDocument.Monitoring.RegisterMaps.Count == 0
                ? bootstrapOptions.RegisterMaps
                : settingsDocument.Monitoring.RegisterMaps,
        };
    }
}
