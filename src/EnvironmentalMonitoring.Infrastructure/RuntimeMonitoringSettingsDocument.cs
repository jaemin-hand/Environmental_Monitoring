namespace EnvironmentalMonitoring.Infrastructure;

public sealed class RuntimeMonitoringSettingsDocument
{
    public MonitoringRuntimeOptions Monitoring { get; set; } = new();

    public List<RuntimeDeviceSetting> Devices { get; set; } = [];

    public List<RuntimeChannelSetting> Channels { get; set; } = [];
}

public sealed class RuntimeDeviceSetting
{
    public string Key { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string IpAddress { get; set; } = string.Empty;

    public int Port { get; set; } = 502;
}

public sealed class RuntimeChannelSetting
{
    public string Code { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string LocationName { get; set; } = string.Empty;

    public decimal? TargetValue { get; set; }

    public decimal? DeviationThreshold { get; set; }

    public decimal? LowAlarmLimit { get; set; }

    public decimal? HighAlarmLimit { get; set; }

    public decimal CalibrationScale { get; set; } = 1m;

    public decimal Offset { get; set; }
}
