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
                DisplayName: "Vaisala Indigo520 + HMP1 + Barometer",
                Protocol: "Modbus TCP",
                IpAddress: "192.168.0.10",
                Port: 502,
                Notes: "HMP1 temperature/humidity probe and built-in barometer over LAN"),
            new DeviceEndpoint(
                Key: "TMW110-RS485",
                DisplayName: "Vaisala TMW110 RS-485 Daisy Chain",
                Protocol: "RS-485 Modbus RTU",
                IpAddress: "COM1",
                Port: 9600,
                Notes: "Five TMW110 temperature sensors connected by RS-485 daisy chain"),
        };

        var channels = new[]
        {
            new MeasurementChannel(1, "T01", "Indigo520", "Indigo520 HMP1 온도", ChannelKind.Temperature, "degC", "INDIGO520", 0, true, 5m, 23m, null, null, 1m, 0m),
            new MeasurementChannel(2, "T02", "T1", "좌측 상단", ChannelKind.Temperature, "degC", "TMW110-RS485", 1, true, 5m, 23m, null, null, 1m, 0m),
            new MeasurementChannel(3, "T03", "T2", "좌측 중단", ChannelKind.Temperature, "degC", "TMW110-RS485", 2, true, 5m, 23m, null, null, 1m, 0m),
            new MeasurementChannel(4, "T04", "T3", "좌측 하단", ChannelKind.Temperature, "degC", "TMW110-RS485", 3, true, 5m, 23m, null, null, 1m, 0m),
            new MeasurementChannel(5, "T05", "T4", "하단 우측", ChannelKind.Temperature, "degC", "TMW110-RS485", 4, true, 5m, 23m, null, null, 1m, 0m),
            new MeasurementChannel(6, "T06", "T5", "전기 패널 하단", ChannelKind.Temperature, "degC", "TMW110-RS485", 5, true, 5m, 23m, null, null, 1m, 0m),
            new MeasurementChannel(7, "H01", "Indigo520 습도", "Indigo520 HMP1 습도", ChannelKind.Humidity, "%RH", "INDIGO520", 1, false, null, null, null, null, 1m, 0m),
            new MeasurementChannel(8, "P01", "Indigo520 대기압", "Indigo520 내장 대기압", ChannelKind.Pressure, "kPa", "INDIGO520", 2, false, null, null, null, null, 1m, 0m),
        };

        return new MonitoringBlueprint(
            DefaultSamplingMode: defaultSamplingMode,
            Devices: Array.AsReadOnly(devices),
            Channels: Array.AsReadOnly(channels));
    }
}
