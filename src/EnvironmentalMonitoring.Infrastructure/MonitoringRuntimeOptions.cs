namespace EnvironmentalMonitoring.Infrastructure;

public sealed class MonitoringRuntimeOptions
{
    public string GatewayMode { get; set; } = "Placeholder";

    public Dictionary<string, ModbusChannelMapOptions> RegisterMaps { get; set; } = [];
}
