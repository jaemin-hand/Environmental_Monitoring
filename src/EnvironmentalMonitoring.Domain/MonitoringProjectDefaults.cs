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
            new MeasurementChannel(1, "T01", "CH1 Temperature", "시험장 중앙 (1.5m)", ChannelKind.Temperature, "degC", "ADAM6015-A", 0, true, 5m, 23m, null, null, 1m, 0m),
            new MeasurementChannel(2, "T02", "CH2 Temperature", "시험장 입구 근처", ChannelKind.Temperature, "degC", "ADAM6015-A", 1, true, 5m, 23m, null, null, 1m, 0m),
            new MeasurementChannel(3, "T03", "CH3 Temperature", "샤시다이나모 근접", ChannelKind.Temperature, "degC", "ADAM6015-A", 2, true, 5m, 23m, null, null, 1m, 0m),
            new MeasurementChannel(4, "T04", "CH4 Temperature", "배기/환기구 근처", ChannelKind.Temperature, "degC", "ADAM6015-A", 3, true, 5m, 23m, null, null, 1m, 0m),
            new MeasurementChannel(5, "T05", "CH5 Temperature", "실험실 구석 A", ChannelKind.Temperature, "degC", "ADAM6015-A", 4, true, 5m, 23m, null, null, 1m, 0m),
            new MeasurementChannel(6, "T06", "CH6 Temperature", "실험실 구석 B", ChannelKind.Temperature, "degC", "ADAM6015-A", 5, true, 5m, 23m, null, null, 1m, 0m),
            new MeasurementChannel(7, "T07", "CH7 Temperature", "실험실 벽면", ChannelKind.Temperature, "degC", "ADAM6015-A", 6, true, 5m, 23m, null, null, 1m, 0m),
            new MeasurementChannel(8, "T08", "CH8 Temperature", "실험실 천장 근처", ChannelKind.Temperature, "degC", "ADAM6015-B", 0, true, 5m, 23m, null, null, 1m, 0m),
            new MeasurementChannel(9, "H01", "H1 Humidity", "습도 설치 위치 협의", ChannelKind.Humidity, "%RH", "INDIGO520", 0, false, null, null, null, null, 1m, 0m),
            new MeasurementChannel(10, "P01", "P1 Pressure", "압력 설치 위치 협의", ChannelKind.Pressure, "kPa", "INDIGO520", 1, false, null, null, null, null, 1m, 0m),
        };

        return new MonitoringBlueprint(
            DefaultSamplingMode: defaultSamplingMode,
            Devices: Array.AsReadOnly(devices),
            Channels: Array.AsReadOnly(channels));
    }
}
