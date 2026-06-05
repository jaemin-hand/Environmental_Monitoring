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

    private static readonly HashSet<string> ObsoleteDefaultChannelCodes = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> PreviousDefaultChannelDisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["T01"] = "Point 1 (HMP1 기준)",
        ["T02"] = "Point 2 (입구)",
        ["T03"] = "Point 3 (센터)",
        ["T04"] = "Point 4 (앞단)",
        ["T05"] = "Point 5",
        ["T06"] = "Point 6",
        ["T07"] = "Point 7",
        ["T08"] = "Point 8",
        ["H01"] = "H1 Humidity",
        ["P01"] = "P1 Pressure",
    };

    private static readonly Dictionary<string, string> PreviousDefaultChannelLocationNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["T01"] = "시험장 중앙 (1.5m)",
        ["T02"] = "시험장 입구 근처",
        ["T03"] = "샤시다이나모 근접",
        ["T04"] = "배기/환기구 근처",
        ["T05"] = "실험실 구석 A",
        ["T06"] = "실험실 구석 B",
        ["T07"] = "실험실 구석 C",
        ["T08"] = "실험실 구석 D",
        ["H01"] = "습도 설치 위치 협의",
        ["P01"] = "압력 설치 위치 협의",
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
                    CalibrationPoints = channel.CalibrationPoints.ToList(),
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

        var removedObsoleteChannels = document.Channels.RemoveAll(
            channel => ObsoleteDefaultChannelCodes.Contains(channel.Code));
        if (removedObsoleteChannels > 0)
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
                    CalibrationPoints = channel.CalibrationPoints.ToList(),
                });
                changed = true;
            }
            else
            {
                var existing = existingChannels[channel.Name];
                if (ShouldUseBlueprintText(existing.DisplayName, PreviousDefaultChannelDisplayNames, channel.Name))
                {
                    existing.DisplayName = channel.DisplayName;
                    changed = true;
                }

                if (ShouldUseBlueprintText(existing.LocationName, PreviousDefaultChannelLocationNames, channel.Name))
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

    private static bool ShouldUseBlueprintText(
        string? currentValue,
        IReadOnlyDictionary<string, string> previousDefaults,
        string channelCode)
    {
        if (string.IsNullOrWhiteSpace(currentValue))
        {
            return true;
        }

        if (string.Equals(currentValue, channelCode, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return previousDefaults.TryGetValue(channelCode, out var previousDefault)
            && string.Equals(currentValue, previousDefault, StringComparison.Ordinal)
            || IsKnownLegacyChannelText(channelCode, currentValue);
    }

    private static bool IsKnownLegacyChannelText(string channelCode, string currentValue)
    {
        string[] legacyValues = channelCode.ToUpperInvariant() switch
        {
            "T01" => ["Indigo520", "Indigo520 HMP1 온도"],
            "T02" => ["T1", "좌측 상단", "Point 2 (입구)", "Point 2(입구)", "Point 2 (연구)", "Point 2(연구)"],
            "T03" => ["T2", "좌측 중단"],
            "T04" => ["T3", "좌측 하단"],
            "T05" => ["T4", "하단 우측"],
            "T06" => ["T5", "전기 패널 하단"],
            "H01" => ["Indigo520 습도", "Indigo520 HMP1 습도"],
            "P01" => ["Indigo520 대기압", "Indigo520 내장 대기압"],
            _ => [],
        };

        return legacyValues.Any(value => string.Equals(
            currentValue,
            value,
            StringComparison.Ordinal));
    }
}
