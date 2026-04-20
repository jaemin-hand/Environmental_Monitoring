namespace EnvironmentalMonitoring.Infrastructure;

public sealed class ModbusChannelMapOptions
{
    public byte UnitId { get; set; } = 1;

    public ushort Address { get; set; }

    public ushort RegisterCount { get; set; } = 1;

    public ModbusRegisterValueType ValueType { get; set; } = ModbusRegisterValueType.UInt16;

    public ModbusWordOrder WordOrder { get; set; } = ModbusWordOrder.HighLow;

    public double Scale { get; set; } = 1.0;

    public double Offset { get; set; }
}
