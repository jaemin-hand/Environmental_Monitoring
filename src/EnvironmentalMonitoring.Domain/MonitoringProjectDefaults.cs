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
                Notes: "Seven TMW110 temperature sensors connected by RS-485 daisy chain"),
        };

        var channels = new[]
        {
            new MeasurementChannel(1, "T01", "Point 1 (HMP1 기준)", "시험장 중앙 (1.5m)", ChannelKind.Temperature, "degC", "INDIGO520", 0, true, 5m, 23m, null, null, 1m, 0m),
            new MeasurementChannel(2, "T02", "Point 2 (입구)", "시험장 입구 근처", ChannelKind.Temperature, "degC", "TMW110-RS485", 1, true, 5m, 23m, null, null, 1m, 0m),
            new MeasurementChannel(3, "T03", "Point 3 (센터)", "샤시다이나모 근접", ChannelKind.Temperature, "degC", "TMW110-RS485", 2, true, 5m, 23m, null, null, 1m, 0m),
            new MeasurementChannel(4, "T04", "Point 4 (앞단)", "배기/환기구 근처", ChannelKind.Temperature, "degC", "TMW110-RS485", 3, true, 5m, 23m, null, null, 1m, 0m),
            new MeasurementChannel(5, "T05", "Point 5", "실험실 구석 A", ChannelKind.Temperature, "degC", "TMW110-RS485", 4, true, 5m, 23m, null, null, 1m, 0m),
            new MeasurementChannel(6, "T06", "Point 6", "실험실 구석 B", ChannelKind.Temperature, "degC", "TMW110-RS485", 5, true, 5m, 23m, null, null, 1m, 0m),
            new MeasurementChannel(7, "T07", "Point 7", "실험실 벽면", ChannelKind.Temperature, "degC", "TMW110-RS485", 6, true, 5m, 23m, null, null, 1m, 0m),
            new MeasurementChannel(8, "T08", "Point 8", "실험실 천장 근처", ChannelKind.Temperature, "degC", "TMW110-RS485", 7, true, 5m, 23m, null, null, 1m, 0m),
            new MeasurementChannel(9, "H01", "H1 Humidity", "습도 설치 위치 협의", ChannelKind.Humidity, "%RH", "INDIGO520", 1, false, null, null, null, null, 1m, 0m),
            new MeasurementChannel(10, "P01", "P1 Pressure", "압력 설치 위치 협의", ChannelKind.Pressure, "kPa", "INDIGO520", 2, false, null, null, null, null, 1m, 0m),
        };

        return new MonitoringBlueprint(
            DefaultSamplingMode: defaultSamplingMode,
            Devices: Array.AsReadOnly(devices),
            Channels: Array.AsReadOnly(channels));
    }
}
