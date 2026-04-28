namespace Ares.Toolkit.Modbus;

/// <summary>
/// Defines the core contract for sending and receiving MODBUS PDU (Protocol Data Unit) frames.
/// </summary>
public interface IModbusTransport : IAsyncDisposable
{
    /// <summary>
    /// Sends a Modbus PDU and waits for the response PDU.
    /// </summary>
    /// <param name="unitId">The Unit Identifier (or Server Address).</param>
    /// <param name="pdu">The raw PDU (Function Code + Data).</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>The raw response PDU.</returns>
    Task<byte[]> SendAndReceiveAsync(byte unitId, byte[] pdu, CancellationToken token = default);
}
