using System.Buffers.Binary;
using System.Net.Sockets;

namespace EnvironmentalMonitoring.Infrastructure;

public sealed class ModbusTcpClient
{
    private int _transactionId;

    public async Task<ushort[]> ReadHoldingRegistersAsync(
        string host,
        int port,
        byte unitId,
        ushort startingAddress,
        ushort registerCount,
        CancellationToken cancellationToken)
    {
        using var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(host, port, cancellationToken);

        await using var stream = tcpClient.GetStream();

        var transactionId = unchecked((ushort)Interlocked.Increment(ref _transactionId));
        var request = BuildReadHoldingRegistersRequest(transactionId, unitId, startingAddress, registerCount);

        await stream.WriteAsync(request, cancellationToken);
        await stream.FlushAsync(cancellationToken);

        var header = await ReadExactAsync(stream, 7, cancellationToken);
        var responseTransactionId = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(0, 2));
        var protocolId = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(2, 2));
        var length = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4, 2));
        var responseUnitId = header[6];

        if (responseTransactionId != transactionId)
        {
            throw new InvalidOperationException("Modbus transaction identifier mismatch.");
        }

        if (protocolId != 0)
        {
            throw new InvalidOperationException("Invalid Modbus protocol identifier.");
        }

        if (responseUnitId != unitId)
        {
            throw new InvalidOperationException("Unexpected Modbus unit identifier in response.");
        }

        var pdu = await ReadExactAsync(stream, length - 1, cancellationToken);
        var functionCode = pdu[0];

        if (functionCode == 0x83)
        {
            throw new InvalidOperationException($"Modbus exception response: code {pdu[1]}.");
        }

        if (functionCode != 0x03)
        {
            throw new InvalidOperationException($"Unexpected Modbus function code: {functionCode}.");
        }

        var expectedByteCount = registerCount * 2;
        var byteCount = pdu[1];

        if (byteCount != expectedByteCount)
        {
            throw new InvalidOperationException("Modbus byte count mismatch.");
        }

        var registers = new ushort[registerCount];
        var registerBytes = pdu.AsSpan(2);

        for (var index = 0; index < registerCount; index++)
        {
            registers[index] = BinaryPrimitives.ReadUInt16BigEndian(registerBytes.Slice(index * 2, 2));
        }

        return registers;
    }

    private static byte[] BuildReadHoldingRegistersRequest(
        ushort transactionId,
        byte unitId,
        ushort startingAddress,
        ushort registerCount)
    {
        var frame = new byte[12];
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(0, 2), transactionId);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(2, 2), 0);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(4, 2), 6);
        frame[6] = unitId;
        frame[7] = 0x03;
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(8, 2), startingAddress);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(10, 2), registerCount);
        return frame;
    }

    private static async Task<byte[]> ReadExactAsync(
        NetworkStream stream,
        int byteCount,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[byteCount];
        var offset = 0;

        while (offset < byteCount)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, byteCount - offset), cancellationToken);

            if (read == 0)
            {
                throw new InvalidOperationException("Modbus connection closed before full response was received.");
            }

            offset += read;
        }

        return buffer;
    }
}
