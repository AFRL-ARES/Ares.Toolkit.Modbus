using Ares.Toolkit.Modbus.Transports;
using System.Net;
using System.Net.Sockets;
using System.Buffers.Binary;

namespace Ares.Toolkit.Modbus.Tests;

[TestFixture]
public class ModbusTcpTransportTests
{
    private TcpListener? _listener;
    private int _port;
    private CancellationTokenSource? _cts;

    [SetUp]
    public void Setup()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        _port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _cts = new CancellationTokenSource();
    }

    [TearDown]
    public void TearDown()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _listener?.Stop();
        _listener?.Dispose();
    }

    private async Task ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken token)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(offset), token);
            if (read == 0) throw new EndOfStreamException();
            offset += read;
        }
    }

    private async Task RunServerAsync(CancellationToken token)
    {
        try
        {
            using var client = await _listener!.AcceptTcpClientAsync(token);
            using var stream = client.GetStream();
            byte[] header = new byte[7];
            
            while (!token.IsCancellationRequested)
            {
                await ReadExactlyAsync(stream, header, token);

                ushort len = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4, 2));
                byte[] pdu = new byte[len - 1];
                await ReadExactlyAsync(stream, pdu, token);

                // Simple echo response: TransactionId, ProtocolId, Length, UnitId, PDU
                byte[] response = new byte[7 + pdu.Length];
                header.CopyTo(response, 0);
                // Length is the same for echo
                pdu.CopyTo(response, 7);

                await stream.WriteAsync(response, token);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Console.WriteLine($"Server error: {ex}"); }
    }

    [Test]
    public async Task SendAndReceiveAsync_WorksWithLocalServer()
    {
        // Arrange
        _ = Task.Run(() => RunServerAsync(_cts!.Token));
        await using var transport = new ModbusTcpTransport("127.0.0.1", _port);
        byte unitId = 1;
        byte[] pdu = { 3, 0, 10, 0, 2 };

        // Act
        var result = await transport.SendAndReceiveAsync(unitId, pdu, _cts!.Token);

        // Assert
        Assert.That(result, Is.EqualTo(pdu));
    }
}
