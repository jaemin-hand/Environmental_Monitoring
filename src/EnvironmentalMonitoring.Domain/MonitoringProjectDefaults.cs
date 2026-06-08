namespace EnvironmentalMonitoring.Domain;

public static class MonitoringProjectDefaults
{
    public static MonitoringBlueprint CreateBlueprint(
        SamplingMode defaultSamplingMode = SamplingMode.OneMinute)
    {
        var devices = new[]
        {
            new DeviceEndpoint(
                Key: "HCD-S-MOD",
                DisplayName: "HCD-S-MOD 습도 센서",
                Protocol: "RS-485 Modbus RTU",
                IpAddress: "COM2",
                Port: 9600,
                Notes: "Humidity sensor over RS-485 Modbus"),
            new DeviceEndpoint(
                Key: "PBS83M00L",
                DisplayName: "PBS83M00L 대기압 센서",
                Protocol: "RS-485 Modbus RTU",
                IpAddress: "COM3",
                Port: 9600,
                Notes: "Barometric pressure sensor over RS-485 Modbus"),
            new DeviceEndpoint(
                Key: "TMW110-RS485",
                DisplayName: "Vaisala TMW110 RS-485 Daisy Chain",
                Protocol: "RS-485 Modbus RTU",
                IpAddress: "COM1",
                Port: 9600,
                Notes: "Eight TMW110 temperature sensors connected by RS-485 daisy chain"),
        };

        var channels = new[]
        {
            new MeasurementChannel(1, "T01", "T1", "온도 포인트 1", ChannelKind.Temperature, "degC", "TMW110-RS485", 1, true, 5m, 23m, null, null, 1m, 0m, []),
            new MeasurementChannel(2, "T02", "T2", "온도 포인트 2", ChannelKind.Temperature, "degC", "TMW110-RS485", 2, true, 5m, 23m, null, null, 1m, 0m, []),
            new MeasurementChannel(3, "T03", "T3", "온도 포인트 3", ChannelKind.Temperature, "degC", "TMW110-RS485", 3, true, 5m, 23m, null, null, 1m, 0m, []),
            new MeasurementChannel(4, "T04", "T4", "온도 포인트 4", ChannelKind.Temperature, "degC", "TMW110-RS485", 4, true, 5m, 23m, null, null, 1m, 0m, []),
            new MeasurementChannel(5, "T05", "T5", "온도 포인트 5", ChannelKind.Temperature, "degC", "TMW110-RS485", 5, true, 5m, 23m, null, null, 1m, 0m, []),
            new MeasurementChannel(6, "T06", "T6", "온도 포인트 6", ChannelKind.Temperature, "degC", "TMW110-RS485", 6, true, 5m, 23m, null, null, 1m, 0m, []),
            new MeasurementChannel(7, "T07", "T7", "온도 포인트 7", ChannelKind.Temperature, "degC", "TMW110-RS485", 7, true, 5m, 23m, null, null, 1m, 0m, []),
            new MeasurementChannel(8, "T08", "T8", "온도 포인트 8", ChannelKind.Temperature, "degC", "TMW110-RS485", 8, true, 5m, 23m, null, null, 1m, 0m, []),
            new MeasurementChannel(9, "H01", "습도", "HCD-S-MOD 습도", ChannelKind.Humidity, "%RH", "HCD-S-MOD", 1, false, null, null, null, null, 1m, 0m, []),
            new MeasurementChannel(10, "P01", "대기압", "PBS83M00L 대기압", ChannelKind.Pressure, "kPa", "PBS83M00L", 1, false, null, null, null, null, 1m, 0m, []),
        };

        return new MonitoringBlueprint(
            DefaultSamplingMode: defaultSamplingMode,
            Devices: Array.AsReadOnly(devices),
            Channels: Array.AsReadOnly(channels));
    }
}
