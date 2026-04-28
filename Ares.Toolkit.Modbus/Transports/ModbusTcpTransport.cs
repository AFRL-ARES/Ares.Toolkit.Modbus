using System.Net.Sockets;
using System.Buffers.Binary;

namespace Ares.Toolkit.Modbus.Transports;

public class ModbusTcpTransport : IModbusTransport
{
    private readonly string _host;
    private readonly int _port;
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private int _transactionId = 0;
    private readonly SemaphoreSlim _lock = new(1);

    public ModbusTcpTransport(string host, int port = 502)
    {
        _host = host;
        _port = port;
    }

    private async Task EnsureConnectedAsync(CancellationToken token)
    {
        if (_tcpClient != null && _tcpClient.Connected) return;

        _tcpClient?.Dispose();
        _tcpClient = new TcpClient();
        await _tcpClient.ConnectAsync(_host, _port, token);
        _stream = _tcpClient.GetStream();
    }

    public async Task<byte[]> SendAndReceiveAsync(byte unitId, byte[] pdu, CancellationToken token = default)
    {
        await _lock.WaitAsync(token);
        try
        {
            await EnsureConnectedAsync(token);

            ushort tid = (ushort)(Interlocked.Increment(ref _transactionId) & 0xFFFF);
            
            // MBAP Header: 
            // Transaction ID (2 bytes)
            // Protocol ID (2 bytes, 0 for Modbus)
            // Length (2 bytes, UnitID + PDU length)
            // Unit ID (1 byte)
            
            byte[] frame = new byte[7 + pdu.Length];
            BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(0, 2), tid);
            BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(2, 2), 0); // Protocol ID
            BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(4, 2), (ushort)(1 + pdu.Length));
            frame[6] = unitId;
            pdu.CopyTo(frame, 7);

            await _stream!.WriteAsync(frame, token);

            // Read MBAP Header (7 bytes)
            byte[] headerBuffer = new byte[7];
            await ReadExactlyAsync(_stream, headerBuffer, token);

            ushort responseTid = BinaryPrimitives.ReadUInt16BigEndian(headerBuffer.AsSpan(0, 2));
            ushort responseLen = BinaryPrimitives.ReadUInt16BigEndian(headerBuffer.AsSpan(4, 2));

            if (responseTid != tid)
                throw new ModbusException($"Transaction ID mismatch. Expected {tid}, got {responseTid}");

            // Read PDU
            byte[] responsePdu = new byte[responseLen - 1]; // Length includes UnitId (1 byte)
            await ReadExactlyAsync(_stream, responsePdu, token);

            return responsePdu;
        }
        catch (Exception ex) when (ex is not ModbusException && ex is not OperationCanceledException)
        {
            _tcpClient?.Dispose();
            _tcpClient = null;
            throw new ModbusException("Error communicating with Modbus TCP server", ex);
        }
        finally
        {
            _lock.Release();
        }
    }

    private static async Task ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken token)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(offset), token);
            if (read == 0) throw new EndOfStreamException();
            offset += read;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _tcpClient?.Dispose();
        _lock.Dispose();
        await ValueTask.CompletedTask;
    }
}
