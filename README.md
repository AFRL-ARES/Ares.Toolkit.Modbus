# Ares.Toolkit.Modbus

A comprehensive MODBUS toolkit for .NET 10, designed for the ARES ecosystem. This library provides a unified high-level API for interacting with MODBUS devices over TCP, RTU (Serial), and ASCII (Serial) protocols.

## Features

- **Protocol Support**: 
  - **Modbus TCP**: Direct communication over Ethernet.
  - **Modbus RTU**: Binary serial communication with CRC-16 validation.
  - **Modbus ASCII**: Text-based serial communication with LRC validation.
- **High-Level Client**: Standardized methods for common Function Codes (FC 01-06, 16).
- **Asynchronous First**: All operations are fully `async` and support `CancellationToken`.
- **Decoupled Architecture**: Easily implement custom or vendor-specific function codes using raw PDU transport.
- **Serial Integration**: Built on top of `Ares.Toolkit.Serial` for robust serial port management and command queueing.

## Usage Examples

### 1. Modbus TCP
Connect to a device over your network.

```csharp
using Ares.Toolkit.Modbus;
using Ares.Toolkit.Modbus.Transports;

// 1. Initialize the TCP transport (Default port is 502)
await using var transport = new ModbusTcpTransport("192.168.1.50", 502);
await using var client = new ModbusClient(transport);

// 2. Read 5 holding registers starting at address 100
// (Returns ushort[] with Big-Endian conversion handled automatically)
ushort[] registers = await client.ReadHoldingRegistersAsync(startAddress: 100, count: 5);

Console.WriteLine($"Register 100: {registers[0]}");
```

### 2. Modbus RTU (Serial)
Connect to a device over a COM port using binary framing.

```csharp
using Ares.Toolkit.Modbus;
using Ares.Toolkit.Modbus.Transports;
using Ares.Toolkit.Serial;

// 1. Setup the serial connection using Ares.Toolkit.Serial
var connectionInfo = new SerialPortConnectionInfo("COM3", 9600, Parity.None, 8, StopBits.One);
using var serialConn = new AresSerialConnection(connectionInfo);
serialConn.AttemptOpen();

// 2. Initialize Modbus RTU transport
await using var transport = new ModbusRtuTransport(serialConn);
await using var client = new ModbusClient(transport);

// 3. Set a coil (digital output) at address 5 to 'On'
await client.WriteSingleCoilAsync(address: 5, value: true);
```

### 3. Modbus ASCII (Serial)
Connect to older or specific hardware requiring ASCII-hex framing.

```csharp
// Similar setup to RTU, just use ModbusAsciiTransport
await using var transport = new ModbusAsciiTransport(serialConn);
await using var client = new ModbusClient(transport);

// Read 8 coils (returns a bool[] after unpacking bits from the ASCII frame)
bool[] coils = await client.ReadCoilsAsync(startAddress: 0, count: 8);
```

## Core Methods

The `ModbusClient` provides access to standard Modbus data types:

| Method | Function Code | Data Type | Description |
| :--- | :--- | :--- | :--- |
| `ReadCoilsAsync` | 01 | `bool[]` | Read/Write digital outputs |
| `ReadDiscreteInputsAsync` | 02 | `bool[]` | Read-only digital inputs |
| `ReadHoldingRegistersAsync` | 03 | `ushort[]` | Read/Write 16-bit parameters |
| `ReadInputRegistersAsync` | 04 | `ushort[]` | Read-only 16-bit sensor data |
| `WriteSingleCoilAsync` | 05 | `void` | Write a single bit |
| `WriteSingleRegisterAsync` | 06 | `void` | Write a single 16-bit value |
| `WriteMultipleRegistersAsync` | 16 (0x10) | `void` | Write a block of 16-bit values |

## Custom Function Codes

If your device uses proprietary function codes, use the `IModbusTransport` directly:

```csharp
byte unitId = 1;
byte[] customPdu = new byte[] { 0x64, 0x01, 0x02 }; // Function 0x64 + Data
byte[] response = await transport.SendAndReceiveAsync(unitId, customPdu);
```

## Error Handling

The library throws specific exceptions to help diagnose communication issues:
- `ModbusServerException`: The device returned a protocol error (includes Function Code and Exception Code).
- `ModbusTimeoutException`: The device failed to respond within the expected timeframe.
- `ModbusException`: General communication or framing errors.
