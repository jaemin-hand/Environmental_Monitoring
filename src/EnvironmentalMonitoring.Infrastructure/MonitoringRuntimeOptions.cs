using EnvironmentalMonitoring.Domain;

namespace EnvironmentalMonitoring.Infrastructure;

public sealed class MonitoringRuntimeOptions
{
    public string GatewayMode { get; set; } = "Placeholder";

    public string DataRoot { get; set; } = "runtime";

    public SamplingMode DefaultSamplingMode { get; set; } = SamplingMode.OneMinute;

    public string PlaceholderProfile { get; set; } = "Stable";

    public int PlaceholderCycleSeconds { get; set; } = 15;

    public Dictionary<string, ModbusChannelMapOptions> RegisterMaps { get; set; } = [];
}
