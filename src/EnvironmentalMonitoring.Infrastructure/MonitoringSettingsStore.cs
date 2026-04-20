using EnvironmentalMonitoring.Domain;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EnvironmentalMonitoring.Infrastructure;

public sealed class MonitoringSettingsStore(MonitoringStorageLayout storageLayout)
{
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
                EnsureMissingEntries(document, runtimeOptions, blueprint);
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
                    DisplayName = BuildChannelDisplayName(channel),
                    TargetValue = channel.TargetValue,
                    DeviationThreshold = channel.DefaultDeviationThreshold,
                    Offset = channel.CalibrationOffset,
                })
                .ToList(),
        };
    }

    private static void EnsureMissingEntries(
        RuntimeMonitoringSettingsDocument document,
        MonitoringRuntimeOptions runtimeOptions,
        MonitoringBlueprint blueprint)
    {
        document.Monitoring ??= new MonitoringRuntimeOptions();
        document.Monitoring.GatewayMode ??= runtimeOptions.GatewayMode;
        document.Monitoring.DataRoot ??= runtimeOptions.DataRoot;

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
                    DisplayName = BuildChannelDisplayName(channel),
                    TargetValue = channel.TargetValue,
                    DeviationThreshold = channel.DefaultDeviationThreshold,
                    Offset = channel.CalibrationOffset,
                });
            }
        }
    }

    private static string BuildChannelDisplayName(MeasurementChannel channel) => channel.Kind switch
    {
        ChannelKind.Temperature => $"CH{channel.ChannelNumber} Temperature",
        ChannelKind.Humidity => "H1 Humidity",
        ChannelKind.Pressure => "P1 Pressure",
        _ => channel.Name,
    };
}
