using System.Buffers.Binary;

namespace EnvironmentalMonitoring.Infrastructure;

internal static class ModbusRegisterDecoder
{
    public static double Decode(
        ushort[] registers,
        ModbusChannelMapOptions map)
    {
        var decoded = map.ValueType switch
        {
            ModbusRegisterValueType.Int16 => unchecked((short)registers[0]),
            ModbusRegisterValueType.UInt16 => registers[0],
            ModbusRegisterValueType.Int32 => DecodeInt32(registers, map.WordOrder),
            ModbusRegisterValueType.UInt32 => DecodeUInt32(registers, map.WordOrder),
            ModbusRegisterValueType.Float32 => DecodeFloat32(registers, map.WordOrder),
            _ => throw new InvalidOperationException($"Unsupported register value type: {map.ValueType}."),
        };

        return decoded * map.Scale + map.Offset;
    }

    private static int DecodeInt32(ushort[] registers, ModbusWordOrder wordOrder)
    {
        Span<byte> bytes = stackalloc byte[4];
        WriteWords(bytes, registers, wordOrder);
        return BinaryPrimitives.ReadInt32BigEndian(bytes);
    }

    private static uint DecodeUInt32(ushort[] registers, ModbusWordOrder wordOrder)
    {
        Span<byte> bytes = stackalloc byte[4];
        WriteWords(bytes, registers, wordOrder);
        return BinaryPrimitives.ReadUInt32BigEndian(bytes);
    }

    private static float DecodeFloat32(ushort[] registers, ModbusWordOrder wordOrder)
    {
        Span<byte> bytes = stackalloc byte[4];
        WriteWords(bytes, registers, wordOrder);
        var bits = BinaryPrimitives.ReadInt32BigEndian(bytes);
        return BitConverter.Int32BitsToSingle(bits);
    }

    private static void WriteWords(
        Span<byte> bytes,
        ushort[] registers,
        ModbusWordOrder wordOrder)
    {
        if (registers.Length < 2)
        {
            throw new InvalidOperationException("Two registers are required for 32-bit Modbus values.");
        }

        var first = wordOrder == ModbusWordOrder.HighLow ? registers[0] : registers[1];
        var second = wordOrder == ModbusWordOrder.HighLow ? registers[1] : registers[0];

        BinaryPrimitives.WriteUInt16BigEndian(bytes.Slice(0, 2), first);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.Slice(2, 2), second);
    }
}
