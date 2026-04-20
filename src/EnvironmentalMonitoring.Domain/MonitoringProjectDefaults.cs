namespace EnvironmentalMonitoring.Domain;

public static class MonitoringProjectDefaults
{
    public static MonitoringBlueprint CreateBlueprint(
        SamplingMode defaultSamplingMode = SamplingMode.OneMinute)
    {
        var devices = new[]
        {
            new DeviceEndpoint(
                Key: "INDIGO520",
                DisplayName: "Vaisala Indigo520",
                Protocol: "Modbus TCP",
                IpAddress: "192.168.0.10",
                Port: 502,
                Notes: "Humidity probe + barometer gateway"),
            new DeviceEndpoint(
                Key: "ADAM6015-A",
                DisplayName: "Advantech ADAM-6015 A",
                Protocol: "Modbus TCP",
                IpAddress: "192.168.0.21",
                Port: 502,
                Notes: "Temperature channels T01-T07"),
            new DeviceEndpoint(
                Key: "ADAM6015-B",
                DisplayName: "Advantech ADAM-6015 B",
                Protocol: "Modbus TCP",
                IpAddress: "192.168.0.22",
                Port: 502,
                Notes: "Temperature channel T08 + spare capacity"),
        };

        var channels = new[]
        {
            new MeasurementChannel(1, "T01", ChannelKind.Temperature, "degC", "ADAM6015-A", 0, true, 5m, 23m, 0m),
            new MeasurementChannel(2, "T02", ChannelKind.Temperature, "degC", "ADAM6015-A", 1, true, 5m, 23m, 0m),
            new MeasurementChannel(3, "T03", ChannelKind.Temperature, "degC", "ADAM6015-A", 2, true, 5m, 23m, 0m),
            new MeasurementChannel(4, "T04", ChannelKind.Temperature, "degC", "ADAM6015-A", 3, true, 5m, 23m, 0m),
            new MeasurementChannel(5, "T05", ChannelKind.Temperature, "degC", "ADAM6015-A", 4, true, 5m, 23m, 0m),
            new MeasurementChannel(6, "T06", ChannelKind.Temperature, "degC", "ADAM6015-A", 5, true, 5m, 23m, 0m),
            new MeasurementChannel(7, "T07", ChannelKind.Temperature, "degC", "ADAM6015-A", 6, true, 5m, 23m, 0m),
            new MeasurementChannel(8, "T08", ChannelKind.Temperature, "degC", "ADAM6015-B", 0, true, 5m, 23m, 0m),
            new MeasurementChannel(9, "H01", ChannelKind.Humidity, "%RH", "INDIGO520", 0, false, null, null, 0m),
            new MeasurementChannel(10, "P01", ChannelKind.Pressure, "kPa", "INDIGO520", 1, false, null, null, 0m),
        };

        return new MonitoringBlueprint(
            DefaultSamplingMode: defaultSamplingMode,
            Devices: Array.AsReadOnly(devices),
            Channels: Array.AsReadOnly(channels));
    }
}
