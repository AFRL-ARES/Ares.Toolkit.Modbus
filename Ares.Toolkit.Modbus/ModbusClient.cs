using System.Buffers.Binary;

namespace Ares.Toolkit.Modbus;

public class ModbusClient : IAsyncDisposable
{
    private readonly IModbusTransport _transport;
    public byte DefaultUnitId { get; set; } = 1;

    public ModbusClient(IModbusTransport transport)
    {
        _transport = transport;
    }

    public async Task<bool[]> ReadCoilsAsync(ushort startAddress, ushort count, byte? unitId = null, CancellationToken token = default)
    {
        byte[] pdu = new byte[5];
        pdu[0] = 1; // Function Code
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1, 2), startAddress);
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3, 2), count);

        byte[] responsePdu = await SendAndReceivePduAsync(unitId ?? DefaultUnitId, pdu, token);
        
        byte byteCount = responsePdu[1];
        bool[] result = new bool[count];
        for (int i = 0; i < count; i++)
        {
            int byteIdx = 2 + (i / 8);
            int bitIdx = i % 8;
            result[i] = (responsePdu[byteIdx] & (1 << bitIdx)) != 0;
        }
        return result;
    }

    public async Task<bool[]> ReadDiscreteInputsAsync(ushort startAddress, ushort count, byte? unitId = null, CancellationToken token = default)
    {
        byte[] pdu = new byte[5];
        pdu[0] = 2; // Function Code
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1, 2), startAddress);
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3, 2), count);

        byte[] responsePdu = await SendAndReceivePduAsync(unitId ?? DefaultUnitId, pdu, token);
        
        byte byteCount = responsePdu[1];
        bool[] result = new bool[count];
        for (int i = 0; i < count; i++)
        {
            int byteIdx = 2 + (i / 8);
            int bitIdx = i % 8;
            result[i] = (responsePdu[byteIdx] & (1 << bitIdx)) != 0;
        }
        return result;
    }

    public async Task<ushort[]> ReadHoldingRegistersAsync(ushort startAddress, ushort count, byte? unitId = null, CancellationToken token = default)
    {
        byte[] pdu = new byte[5];
        pdu[0] = 3; // Function Code
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1, 2), startAddress);
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3, 2), count);

        byte[] responsePdu = await SendAndReceivePduAsync(unitId ?? DefaultUnitId, pdu, token);
        
        byte byteCount = responsePdu[1];
        ushort[] result = new ushort[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = BinaryPrimitives.ReadUInt16BigEndian(responsePdu.AsSpan(2 + i * 2, 2));
        }
        return result;
    }

    public async Task<ushort[]> ReadInputRegistersAsync(ushort startAddress, ushort count, byte? unitId = null, CancellationToken token = default)
    {
        byte[] pdu = new byte[5];
        pdu[0] = 4; // Function Code
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1, 2), startAddress);
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3, 2), count);

        byte[] responsePdu = await SendAndReceivePduAsync(unitId ?? DefaultUnitId, pdu, token);
        
        byte byteCount = responsePdu[1];
        ushort[] result = new ushort[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = BinaryPrimitives.ReadUInt16BigEndian(responsePdu.AsSpan(2 + i * 2, 2));
        }
        return result;
    }

    public async Task WriteSingleCoilAsync(ushort address, bool value, byte? unitId = null, CancellationToken token = default)
    {
        byte[] pdu = new byte[5];
        pdu[0] = 5; // Function Code
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1, 2), address);
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3, 2), (ushort)(value ? 0xFF00 : 0x0000));

        await SendAndReceivePduAsync(unitId ?? DefaultUnitId, pdu, token);
    }

    public async Task WriteSingleRegisterAsync(ushort address, ushort value, byte? unitId = null, CancellationToken token = default)
    {
        byte[] pdu = new byte[5];
        pdu[0] = 6; // Function Code
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1, 2), address);
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3, 2), value);

        await SendAndReceivePduAsync(unitId ?? DefaultUnitId, pdu, token);
    }

    public async Task WriteMultipleRegistersAsync(ushort startAddress, ushort[] values, byte? unitId = null, CancellationToken token = default)
    {
        byte[] pdu = new byte[6 + values.Length * 2];
        pdu[0] = 16; // Function Code (0x10)
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1, 2), startAddress);
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3, 2), (ushort)values.Length);
        pdu[5] = (byte)(values.Length * 2);
        for (int i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(6 + i * 2, 2), values[i]);
        }

        await SendAndReceivePduAsync(unitId ?? DefaultUnitId, pdu, token);
    }

    private async Task<byte[]> SendAndReceivePduAsync(byte unitId, byte[] pdu, CancellationToken token)
    {
        byte[] responsePdu = await _transport.SendAndReceiveAsync(unitId, pdu, token);

        if (responsePdu.Length < 1)
            throw new ModbusException("Received empty response PDU");

        byte functionCode = responsePdu[0];
        if ((functionCode & 0x80) != 0)
        {
            byte exceptionCode = responsePdu.Length > 1 ? responsePdu[1] : (byte)0;
            throw new ModbusServerException(pdu[0], exceptionCode);
        }

        if (functionCode != pdu[0])
            throw new ModbusException($"Function code mismatch. Expected {pdu[0]}, got {functionCode}");

        return responsePdu;
    }

    public async ValueTask DisposeAsync()
    {
        await _transport.DisposeAsync();
    }
}
