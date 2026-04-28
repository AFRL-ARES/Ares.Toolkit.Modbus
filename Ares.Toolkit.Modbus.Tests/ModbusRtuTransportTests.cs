using Ares.Toolkit.Modbus.Transports;
using Ares.Toolkit.Serial;
using Ares.Toolkit.Serial.Simulation;
using NUnit.Framework;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Linq;

namespace Ares.Toolkit.Modbus.Tests;

[TestFixture]
public class ModbusRtuTransportTests
{
    private class ModbusSimConnection : AresSerialSimConnection
    {
        public ModbusSimConnection() : base(new SerialPortConnectionInfo(9600, Parity.None, 8, StopBits.One), "ModbusSim")
        {
        }

        public override void SendInternally(byte[] bytes)
        {
            if (bytes.Length < 2) return;
            byte unitId = bytes[0];
            byte fc = bytes[1];

            byte[] responsePdu;
            if (fc == 3)
            {
                // Response for Read Holding Registers: FC, ByteCount, Data...
                responsePdu = new byte[] { 3, 4, 0, 123, 0, 200 };
            }
            else
            {
                // Default echo for others (might still fail length check if not handled)
                responsePdu = bytes.AsSpan(1, bytes.Length - 3).ToArray();
            }

            byte[] frame = new byte[responsePdu.Length + 3];
            frame[0] = unitId;
            responsePdu.CopyTo(frame, 1);
            ushort crc = CalculateCrc(frame.AsSpan(0, frame.Length - 2));
            frame[frame.Length - 2] = (byte)(crc & 0xFF);
            frame[frame.Length - 1] = (byte)(crc >> 8);

            AddDataReceived(frame);
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

    [Test]
    public async Task SendAndReceiveAsync_WorksWithSimulatedConnection()
    {
        // Arrange
        await using var connection = new ModbusSimConnection();
        connection.AttemptOpen();
        var transport = new ModbusRtuTransport(connection);
        byte unitId = 1;
        byte[] pdu = { 3, 0, 10, 0, 2 };
        byte[] expectedResponsePdu = { 3, 4, 0, 123, 0, 200 };

        // Act
        var result = await transport.SendAndReceiveAsync(unitId, pdu);

        // Assert
        Assert.That(result, Is.EqualTo(expectedResponsePdu));
    }
}
