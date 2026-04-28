namespace Ares.Toolkit.Modbus;

/// <summary>
/// Base class for Modbus-related exceptions.
/// </summary>
public class ModbusException : Exception
{
    public ModbusException(string message) : base(message) { }
    public ModbusException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception thrown when a Modbus device returns an error code.
/// </summary>
public class ModbusServerException : ModbusException
{
    public byte FunctionCode { get; }
    public byte ExceptionCode { get; }

    public ModbusServerException(byte functionCode, byte exceptionCode)
        : base($"Modbus server returned error. Function Code: 0x{functionCode:X2}, Exception Code: 0x{exceptionCode:X2}")
    {
        FunctionCode = functionCode;
        ExceptionCode = exceptionCode;
    }
}

/// <summary>
/// Exception thrown when a Modbus operation times out.
/// </summary>
public class ModbusTimeoutException : ModbusException
{
    public ModbusTimeoutException(string message) : base(message) { }
}
