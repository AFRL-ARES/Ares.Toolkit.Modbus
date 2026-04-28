using Ares.Toolkit.Serial;
using Ares.Toolkit.Serial.Commands;
using System.Buffers.Binary;

namespace Ares.Toolkit.Modbus.Transports;

public class ModbusRtuTransport : IModbusTransport
{
    private readonly IAresSerialConnection _connection;

    public ModbusRtuTransport(IAresSerialConnection connection)
    {
        _connection = connection;
    }

    public async Task<byte[]> SendAndReceiveAsync(byte unitId, byte[] pdu, CancellationToken token = default)
    {
        var command = new ModbusRtuCommand(unitId, pdu);
        var response = await _connection.Send(command, token);
        return response.Pdu;
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    private class ModbusRtuResponse : SerialResponse
    {
        public byte[] Pdu { get; }
        public ModbusRtuResponse(byte[] pdu)
        {
            Pdu = pdu;
        }
    }

    private class ModbusRtuResponseParser : SerialResponseParser<ModbusRtuResponse>
    {
        private readonly byte _unitId;
        private readonly byte _functionCode;

        public ModbusRtuResponseParser(byte unitId, byte functionCode)
        {
            _unitId = unitId;
            _functionCode = functionCode;
        }

        public override bool TryParseResponse(byte[] buffer, out ModbusRtuResponse? response, out ArraySegment<byte>? dataToRemove)
        {
            response = null;
            dataToRemove = null;

            if (buffer.Length < 4) return false;

            // Find UnitId and FunctionCode in buffer
            // Since RTU doesn't have a unique start character, we look for the expected UnitId and FunctionCode
            // Note: This is a bit simplified, a real RTU parser might need timing-based framing (3.5 char time)
            // but the toolkit handles buffering.
            
            for (int i = 0; i <= buffer.Length - 4; i++)
            {
                if (buffer[i] == _unitId && (buffer[i + 1] == _functionCode || buffer[i + 1] == (_functionCode | 0x80)))
                {
                    byte functionCode = buffer[i + 1];
                    int expectedLength = GetExpectedResponseLength(functionCode, buffer.AsSpan(i));
                    
                    if (expectedLength == -1) continue; // Unknown or not enough data to determine length
                    
                    if (i + expectedLength > buffer.Length) return false; // Need more data

                    // Verify CRC
                    ushort receivedCrc = BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(i + expectedLength - 2, 2));
                    ushort calculatedCrc = CalculateCrc(buffer.AsSpan(i, expectedLength - 2));

                    if (receivedCrc == calculatedCrc)
                    {
                        byte[] pdu = new byte[expectedLength - 3]; // Minus UnitId (1) and CRC (2)
                        buffer.AsSpan(i + 1, expectedLength - 3).CopyTo(pdu);
                        
                        response = new ModbusRtuResponse(pdu);
                        dataToRemove = new ArraySegment<byte>(buffer, 0, i + expectedLength);
                        return true;
                    }
                }
            }

            return false;
        }

        private static int GetExpectedResponseLength(byte functionCode, ReadOnlySpan<byte> framePart)
        {
            // Error response
            if ((functionCode & 0x80) != 0) return 5; // UnitId(1) + ErrorFC(1) + ExCode(1) + CRC(2)

            return functionCode switch
            {
                1 or 2 or 3 or 4 => framePart.Length > 2 ? framePart[2] + 5 : -1, // UnitId(1)+FC(1)+ByteCount(1)+Data(N)+CRC(2)
                5 or 6 or 15 or 16 => 8, // Fixed length: UnitId(1)+FC(1)+Addr(2)+Val/Count(2)+CRC(2)
                _ => -1
            };
        }

        private static ushort CalculateCrc(ReadOnlySpan<byte> data)
        {
            ushort crc = 0xFFFF;
            foreach (byte b in data)
            {
                crc ^= b;
                for (int i = 0; i < 8; i++)
                {
                    if ((crc & 0x0001) != 0)
                    {
                        crc >>= 1;
                        crc ^= 0xA001;
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }
            }
            return crc;
        }
    }

    private class ModbusRtuCommand : SerialCommandWithResponse<ModbusRtuResponse>
    {
        private readonly byte _unitId;
        private readonly byte[] _pdu;

        public ModbusRtuCommand(byte unitId, byte[] pdu) 
            : base(new ModbusRtuResponseParser(unitId, pdu[0]))
        {
            _unitId = unitId;
            _pdu = pdu;
        }

        protected override byte[] Serialize()
        {
            byte[] frame = new byte[_pdu.Length + 3];
            frame[0] = _unitId;
            _pdu.CopyTo(frame, 1);
            ushort crc = CalculateCrc(frame.AsSpan(0, frame.Length - 2));
            BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(frame.Length - 2), crc);
            return frame;
        }

        private static ushort CalculateCrc(ReadOnlySpan<byte> data)
        {
            ushort crc = 0xFFFF;
            foreach (byte b in data)
            {
                crc ^= b;
                for (int i = 0; i < 8; i++)
                {
                    if ((crc & 0x0001) != 0)
                    {
                        crc >>= 1;
                        crc ^= 0xA001;
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }
            }
            return crc;
        }
    }
}
