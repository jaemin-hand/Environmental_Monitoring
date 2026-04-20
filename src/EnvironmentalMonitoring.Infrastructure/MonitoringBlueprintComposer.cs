using EnvironmentalMonitoring.Domain;

namespace EnvironmentalMonitoring.Infrastructure;

public static class MonitoringBlueprintComposer
{
    public static MonitoringBlueprint Compose(
        MonitoringRuntimeOptions runtimeOptions,
        RuntimeMonitoringSettingsDocument settingsDocument)
    {
        var defaults = MonitoringProjectDefaults.CreateBlueprint(runtimeOptions.DefaultSamplingMode);

        var deviceOverrides = settingsDocument.Devices
            .ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);
        var channelOverrides = settingsDocument.Channels
            .ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);

        var devices = defaults.Devices
            .Select(device =>
            {
                if (!deviceOverrides.TryGetValue(device.Key, out var overrideItem))
                {
                    return device;
                }

                return device with
                {
                    DisplayName = string.IsNullOrWhiteSpace(overrideItem.DisplayName)
                        ? device.DisplayName
                        : overrideItem.DisplayName,
                    IpAddress = string.IsNullOrWhiteSpace(overrideItem.IpAddress)
                        ? device.IpAddress
                        : overrideItem.IpAddress,
                    Port = overrideItem.Port <= 0 ? device.Port : overrideItem.Port,
                };
            })
            .ToArray();

        var channels = defaults.Channels
            .Select(channel =>
            {
                if (!channelOverrides.TryGetValue(channel.Name, out var overrideItem))
                {
                    return channel;
                }

                return channel with
                {
                    DefaultDeviationThreshold = overrideItem.DeviationThreshold ?? channel.DefaultDeviationThreshold,
                    TargetValue = overrideItem.TargetValue ?? channel.TargetValue,
                    CalibrationOffset = overrideItem.Offset,
                };
            })
            .ToArray();

        return new MonitoringBlueprint(
            runtimeOptions.DefaultSamplingMode,
            Array.AsReadOnly(devices),
            Array.AsReadOnly(channels));
    }
}
