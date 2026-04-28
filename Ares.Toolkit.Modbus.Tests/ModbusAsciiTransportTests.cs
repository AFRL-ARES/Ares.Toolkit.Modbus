using Ares.Toolkit.Modbus.Transports;
using Ares.Toolkit.Serial;
using Ares.Toolkit.Serial.Simulation;
using NUnit.Framework;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Linq;

namespace Ares.Toolkit.Modbus.Tests;

[TestFixture]
public class ModbusAsciiTransportTests
{
    private class ModbusSimConnection : AresSerialSimConnection
    {
        public ModbusSimConnection() : base(new SerialPortConnectionInfo(9600, Parity.None, 8, StopBits.One), "ModbusSim")
        {
        }

        public override void SendInternally(byte[] bytes)
        {
            string content = System.Text.Encoding.ASCII.GetString(bytes);
            if (!content.StartsWith(":") || !content.EndsWith("\r\n")) return;
            
            string hex = content.Substring(1, content.Length - 3);
            byte[] requestBytes = HexToBytes(hex);
            byte unitId = requestBytes[0];
            byte fc = requestBytes[1];

            byte[] responsePdu;
            if (fc == 3)
            {
                responsePdu = new byte[] { 3, 4, 0, 123, 0, 200 };
            }
            else
            {
                responsePdu = requestBytes.AsSpan(1, requestBytes.Length - 2).ToArray();
            }

            byte[] frameData = new byte[responsePdu.Length + 1];
            frameData[0] = unitId;
            responsePdu.CopyTo(frameData, 1);
            byte lrc = CalculateLrc(frameData);

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append(':');
            foreach (byte b in frameData) sb.Append(b.ToString("X2"));
            sb.Append(lrc.ToString("X2"));
            sb.Append("\r\n");

            AddDataReceived(System.Text.Encoding.ASCII.GetBytes(sb.ToString()));
        }

        private static byte[] HexToBytes(string hex)
        {
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = byte.Parse(hex.Substring(i * 2, 2), System.Globalization.NumberStyles.HexNumber);
            }
            return bytes;
        }

        private static byte CalculateLrc(ReadOnlySpan<byte> data)
        {
            byte lrc = 0;
            foreach (byte b in data) lrc += b;
            return (byte)((lrc ^ 0xFF) + 1);
        }
    }

    [Test]
    public async Task SendAndReceiveAsync_WorksWithSimulatedConnection()
    {
        // Arrange
        await using var connection = new ModbusSimConnection();
        connection.AttemptOpen();
        var transport = new ModbusAsciiTransport(connection);
        byte unitId = 1;
        byte[] pdu = { 3, 0, 10, 0, 2 };
        byte[] expectedResponsePdu = { 3, 4, 0, 123, 0, 200 };

        // Act
        var result = await transport.SendAndReceiveAsync(unitId, pdu);

        // Assert
        Assert.That(result, Is.EqualTo(expectedResponsePdu));
    }
}
