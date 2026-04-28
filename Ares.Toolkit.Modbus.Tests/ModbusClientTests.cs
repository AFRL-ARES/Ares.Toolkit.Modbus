using Moq;
using NUnit.Framework;
using Ares.Toolkit.Modbus.Transports;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace Ares.Toolkit.Modbus.Tests;

[TestFixture]
public class ModbusClientTests
{
    private Mock<IModbusTransport> _transportMock;
    private ModbusClient _client;

    [SetUp]
    public void Setup()
    {
        _transportMock = new Mock<IModbusTransport>();
        _client = new ModbusClient(_transportMock.Object);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _client.DisposeAsync();
    }

    [Test]
    public async Task ReadHoldingRegistersAsync_ReturnsCorrectValues()
    {
        // Arrange
        byte unitId = 1;
        ushort startAddress = 10;
        ushort count = 2;
        byte[] expectedPdu = { 3, 0, 10, 0, 2 };
        byte[] responsePdu = { 3, 4, 0, 123, 0, 200 }; // FC 3, 4 bytes, values 123 and 200

        _transportMock.Setup(t => t.SendAndReceiveAsync(unitId, It.Is<byte[]>(p => Enumerable.SequenceEqual(p, expectedPdu)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(responsePdu);

        // Act
        var result = await _client.ReadHoldingRegistersAsync(startAddress, count, unitId);

        // Assert
        Assert.That(result.Length, Is.EqualTo(2));
        Assert.That(result[0], Is.EqualTo(123));
        Assert.That(result[1], Is.EqualTo(200));
    }

    [Test]
    public void ReadHoldingRegistersAsync_ThrowsModbusServerException_OnError()
    {
        // Arrange
        byte unitId = 1;
        byte[] responsePdu = { 0x83, 0x02 }; // FC 3 error, Illegal Data Address

        _transportMock.Setup(t => t.SendAndReceiveAsync(unitId, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(responsePdu);

        // Act & Assert
        var ex = Assert.ThrowsAsync<ModbusServerException>(async () => await _client.ReadHoldingRegistersAsync(0, 1, unitId));
        Assert.That(ex.FunctionCode, Is.EqualTo(3));
        Assert.That(ex.ExceptionCode, Is.EqualTo(2));
    }

    [Test]
    public async Task WriteSingleRegisterAsync_SendsCorrectPdu()
    {
        // Arrange
        byte unitId = 1;
        ushort address = 5;
        ushort value = 1000;
        byte[] expectedPdu = { 6, 0, 5, 3, 232 }; // 1000 = 0x03E8
        byte[] responsePdu = { 6, 0, 5, 3, 232 }; // Echo

        _transportMock.Setup(t => t.SendAndReceiveAsync(unitId, It.Is<byte[]>(p => Enumerable.SequenceEqual(p, expectedPdu)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(responsePdu);

        // Act
        await _client.WriteSingleRegisterAsync(address, value, unitId);

        // Assert
        _transportMock.VerifyAll();
    }
}
