using Ares.Toolkit.Serial;
using Ares.Toolkit.Serial.Commands;
using System.Text;
using System.Globalization;

namespace Ares.Toolkit.Modbus.Transports;

public class ModbusAsciiTransport : IModbusTransport
{
    private readonly IAresSerialConnection _connection;

    public ModbusAsciiTransport(IAresSerialConnection connection)
    {
        _connection = connection;
    }

    public async Task<byte[]> SendAndReceiveAsync(byte unitId, byte[] pdu, CancellationToken token = default)
    {
        var command = new ModbusAsciiCommand(unitId, pdu);
        var response = await _connection.Send(command, token);
        return response.Pdu;
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    private class ModbusAsciiResponse : SerialResponse
    {
        public byte[] Pdu { get; }
        public ModbusAsciiResponse(byte[] pdu)
        {
            Pdu = pdu;
        }
    }

    private class ModbusAsciiResponseParser : SerialResponseParser<ModbusAsciiResponse>
    {
        private readonly byte _unitId;
        private readonly byte _functionCode;

        public ModbusAsciiResponseParser(byte unitId, byte functionCode)
        {
            _unitId = unitId;
            _functionCode = functionCode;
        }

        public override bool TryParseResponse(byte[] buffer, out ModbusAsciiResponse? response, out ArraySegment<byte>? dataToRemove)
        {
            response = null;
            dataToRemove = null;

            string content = Encoding.ASCII.GetString(buffer);
            int startIdx = content.IndexOf(':');
            if (startIdx < 0) return false;

            int endIdx = content.IndexOf("\r\n", startIdx);
            if (endIdx < 0) return false;

            string frameHex = content.Substring(startIdx + 1, endIdx - startIdx - 1);
            if (frameHex.Length % 2 != 0) return false;

            byte[] frameBytes = HexToBytes(frameHex);
            if (frameBytes.Length < 3) return false;

            byte receivedUnitId = frameBytes[0];
            byte receivedFunctionCode = frameBytes[1];

            if (receivedUnitId != _unitId) return false;
            // Should also check function code or error function code
            if (receivedFunctionCode != _functionCode && receivedFunctionCode != (_functionCode | 0x80)) return false;

            byte receivedLrc = frameBytes[^1];
            byte calculatedLrc = CalculateLrc(frameBytes.AsSpan(0, frameBytes.Length - 1));

            if (receivedLrc != calculatedLrc) return false;

            byte[] pdu = new byte[frameBytes.Length - 2]; // Minus UnitId(1) and LRC(1)
            frameBytes.AsSpan(1, frameBytes.Length - 2).CopyTo(pdu);

            response = new ModbusAsciiResponse(pdu);
            dataToRemove = new ArraySegment<byte>(buffer, 0, endIdx + 2);
            return true;
        }

        private static byte[] HexToBytes(string hex)
        {
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = byte.Parse(hex.Substring(i * 2, 2), NumberStyles.HexNumber);
            }
            return bytes;
        }

        private static byte CalculateLrc(ReadOnlySpan<byte> data)
        {
            byte lrc = 0;
            foreach (byte b in data)
            {
                lrc += b;
            }
            return (byte)((lrc ^ 0xFF) + 1);
        }
    }

    private class ModbusAsciiCommand : SerialCommandWithResponse<ModbusAsciiResponse>
    {
        private readonly byte _unitId;
        private readonly byte[] _pdu;

        public ModbusAsciiCommand(byte unitId, byte[] pdu) 
            : base(new ModbusAsciiResponseParser(unitId, pdu[0]))
        {
            _unitId = unitId;
            _pdu = pdu;
        }

        protected override byte[] Serialize()
        {
            byte[] frameData = new byte[_pdu.Length + 1];
            frameData[0] = _unitId;
            _pdu.CopyTo(frameData, 1);

            byte lrc = CalculateLrc(frameData);
            
            StringBuilder sb = new StringBuilder();
            sb.Append(':');
            foreach (byte b in frameData) sb.Append(b.ToString("X2"));
            sb.Append(lrc.ToString("X2"));
            sb.Append("\r\n");

            return Encoding.ASCII.GetBytes(sb.ToString());
        }

        private static byte CalculateLrc(ReadOnlySpan<byte> data)
        {
            byte lrc = 0;
            foreach (byte b in data)
            {
                lrc += b;
            }
            return (byte)((lrc ^ 0xFF) + 1);
        }
    }
}
