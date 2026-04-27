using EnvironmentalMonitoring.Domain;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EnvironmentalMonitoring.Infrastructure;

public sealed class MonitoringSettingsStore(MonitoringStorageLayout storageLayout)
{
    private static readonly HashSet<string> ObsoleteDefaultDeviceKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "ADAM6015-A",
        "ADAM6015-B",
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter(),
        },
    };

    public string SettingsFilePath => storageLayout.RuntimeSettingsFilePath;

    public RuntimeMonitoringSettingsDocument LoadOrCreateDefaults(
        MonitoringRuntimeOptions runtimeOptions,
        MonitoringBlueprint blueprint)
    {
        storageLayout.EnsureCreated();

        if (File.Exists(SettingsFilePath))
        {
            var json = File.ReadAllText(SettingsFilePath);
            var document = JsonSerializer.Deserialize<RuntimeMonitoringSettingsDocument>(json, JsonOptions);
            if (document is not null)
            {
                var changed = EnsureMissingEntries(document, runtimeOptions, blueprint);
                if (changed)
                {
                    Save(document);
                }

                return document;
            }
        }

        var defaults = CreateDefaultDocument(runtimeOptions, blueprint);
        Save(defaults);
        return defaults;
    }

    public void Save(RuntimeMonitoringSettingsDocument document)
    {
        storageLayout.EnsureCreated();
        var json = JsonSerializer.Serialize(document, JsonOptions);
        File.WriteAllText(SettingsFilePath, json);
    }

    private static RuntimeMonitoringSettingsDocument CreateDefaultDocument(
        MonitoringRuntimeOptions runtimeOptions,
        MonitoringBlueprint blueprint)
    {
        return new RuntimeMonitoringSettingsDocument
        {
            Monitoring = new MonitoringRuntimeOptions
            {
                GatewayMode = runtimeOptions.GatewayMode,
                DataRoot = runtimeOptions.DataRoot,
                DefaultSamplingMode = runtimeOptions.DefaultSamplingMode,
                PlaceholderProfile = runtimeOptions.PlaceholderProfile,
                PlaceholderCycleSeconds = runtimeOptions.PlaceholderCycleSeconds,
                RegisterMaps = runtimeOptions.RegisterMaps,
            },
            Devices = blueprint.Devices
                .Select(device => new RuntimeDeviceSetting
                {
                    Key = device.Key,
                    DisplayName = device.DisplayName,
                    IpAddress = device.IpAddress,
                    Port = device.Port,
                })
                .ToList(),
            Channels = blueprint.Channels
                .Select(channel => new RuntimeChannelSetting
                {
                    Code = channel.Name,
                    DisplayName = channel.DisplayName,
                    LocationName = channel.LocationName,
                    TargetValue = channel.TargetValue,
                    DeviationThreshold = channel.DefaultDeviationThreshold,
                    LowAlarmLimit = channel.LowAlarmLimit,
                    HighAlarmLimit = channel.HighAlarmLimit,
                    CalibrationScale = channel.CalibrationScale,
                    Offset = channel.CalibrationOffset,
                })
                .ToList(),
        };
    }

    private static bool EnsureMissingEntries(
        RuntimeMonitoringSettingsDocument document,
        MonitoringRuntimeOptions runtimeOptions,
        MonitoringBlueprint blueprint)
    {
        var changed = false;

        document.Monitoring ??= new MonitoringRuntimeOptions();
        if (string.IsNullOrWhiteSpace(document.Monitoring.GatewayMode))
        {
            document.Monitoring.GatewayMode = runtimeOptions.GatewayMode;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(document.Monitoring.DataRoot))
        {
            document.Monitoring.DataRoot = runtimeOptions.DataRoot;
            changed = true;
        }

        var removedObsoleteDevices = document.Devices.RemoveAll(
            device => ObsoleteDefaultDeviceKeys.Contains(device.Key));
        if (removedObsoleteDevices > 0)
        {
            changed = true;
        }

        var existingDevices = document.Devices.ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);
        foreach (var device in blueprint.Devices)
        {
            if (!existingDevices.ContainsKey(device.Key))
            {
                document.Devices.Add(new RuntimeDeviceSetting
                {
                    Key = device.Key,
                    DisplayName = device.DisplayName,
                    IpAddress = device.IpAddress,
                    Port = device.Port,
                });
                changed = true;
            }
        }

        var existingChannels = document.Channels.ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);
        foreach (var channel in blueprint.Channels)
        {
            if (!existingChannels.ContainsKey(channel.Name))
            {
                document.Channels.Add(new RuntimeChannelSetting
                {
                    Code = channel.Name,
                    DisplayName = channel.DisplayName,
                    LocationName = channel.LocationName,
                    TargetValue = channel.TargetValue,
                    DeviationThreshold = channel.DefaultDeviationThreshold,
                    LowAlarmLimit = channel.LowAlarmLimit,
                    HighAlarmLimit = channel.HighAlarmLimit,
                    CalibrationScale = channel.CalibrationScale,
                    Offset = channel.CalibrationOffset,
                });
                changed = true;
            }
            else
            {
                var existing = existingChannels[channel.Name];
                if (string.IsNullOrWhiteSpace(existing.DisplayName))
                {
                    existing.DisplayName = channel.DisplayName;
                    changed = true;
                }

                if (string.IsNullOrWhiteSpace(existing.LocationName))
                {
                    existing.LocationName = channel.LocationName;
                    changed = true;
                }

                if (existing.TargetValue is null && channel.TargetValue is not null)
                {
                    existing.TargetValue = channel.TargetValue;
                    changed = true;
                }

                if (existing.DeviationThreshold is null && channel.DefaultDeviationThreshold is not null)
                {
                    existing.DeviationThreshold = channel.DefaultDeviationThreshold;
                    changed = true;
                }

                if (existing.LowAlarmLimit is null && channel.LowAlarmLimit is not null)
                {
                    existing.LowAlarmLimit = channel.LowAlarmLimit;
                    changed = true;
                }

                if (existing.HighAlarmLimit is null && channel.HighAlarmLimit is not null)
                {
                    existing.HighAlarmLimit = channel.HighAlarmLimit;
                    changed = true;
                }

                if (existing.CalibrationScale == 0m)
                {
                    existing.CalibrationScale = channel.CalibrationScale;
                    changed = true;
                }
            }
        }

        return changed;
    }
}
