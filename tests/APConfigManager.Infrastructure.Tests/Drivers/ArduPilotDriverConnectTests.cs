using APConfigManager.Core.Enums;
using APConfigManager.Core.Exceptions;
using APConfigManager.Core.Interfaces.Drivers;
using APConfigManager.Core.Interfaces.Transport;
using APConfigManager.Core.Models;
using APConfigManager.Core.Results;
using APConfigManager.Infrastructure.Drivers.Ardupilot;
using FluentAssertions;
using Moq;

namespace APConfigManager.Infrastructure.Tests.Drivers;

public class ArduPilotDriverConnectTests
{
    private readonly Mock<ISerialPortAdapter> mockPort;
    private readonly Mock<IBootloaderProtocol> mockBootloader;
    private readonly Mock<ITelemetryProtocol> mockTelemetry;
    private readonly Mock<IPortScanner> mockPortScanner;
    private readonly ArduPilotDriver driver;

    public ArduPilotDriverConnectTests()
    {
        mockPort = new Mock<ISerialPortAdapter>();
        mockBootloader = new Mock<IBootloaderProtocol>();
        mockTelemetry = new Mock<ITelemetryProtocol>();
        mockPortScanner = new Mock<IPortScanner>();

        mockPort.Setup(p => p.IsOpen).Returns(true);

        driver = new ArduPilotDriver(
            mockPort.Object,
            mockBootloader.Object,
            mockTelemetry.Object,
            mockPortScanner.Object);
    }

    // ─── ConnectAsync: saves device serial ──────

    [Fact]
    public async Task ConnectAsync_NormalMode_SavesDeviceSerial()
    {
        mockTelemetry
            .Setup(t => t.SendHeartbeatAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        mockTelemetry
            .Setup(t => t.WaitForHeartbeatAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        mockPortScanner
            .Setup(s => s.GetPortDescription("COM9"))
            .Returns(new PortDescription
            {
                Name = "COM9",
                Description = "Cube Orange+ Mavlink",
                DeviceSerial = "AAA111",
                VendorId = "2DAE",
                ProductId = "1058",
                IsMavlink = true
            });

        var session = await driver.ConnectAsync("COM9", 115200, CancellationToken.None);

        session.DeviceSerial.Should().Be("AAA111");
        session.Port.Should().Be("COM9");
        session.State.Should().Be(DeviceState.Connected);
    }

    [Fact]
    public async Task ConnectAsync_BootloaderMode_SavesDeviceSerial()
    {
        // SendHeartbeat succeeds — it just sends bytes
        mockTelemetry
            .Setup(t => t.SendHeartbeatAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // But no heartbeat response — device is in bootloader
        mockTelemetry
            .Setup(t => t.WaitForHeartbeatAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        mockBootloader
            .Setup(b => b.SyncAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        mockBootloader
            .Setup(b => b.GetDeviceInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeviceInfo { BoardId = 140, FlashSize = 2097152 });

        mockPortScanner
            .Setup(s => s.GetPortDescription("COM11"))
            .Returns(new PortDescription
            {
                Name = "COM11",
                Description = "STM32 Bootloader",
                DeviceSerial = "AAA111",
                IsMavlink = false
            });

        var session = await driver.ConnectAsync("COM11", 115200, CancellationToken.None);

        session.DeviceSerial.Should().Be("AAA111");
        session.State.Should().Be(DeviceState.InBootloader);
    }

    [Fact]
    public async Task ConnectAsync_NoPortDescription_EmptySerial()
    {
        mockTelemetry
            .Setup(t => t.SendHeartbeatAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        mockTelemetry
            .Setup(t => t.WaitForHeartbeatAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        mockPortScanner
            .Setup(s => s.GetPortDescription(It.IsAny<string>()))
            .Returns((PortDescription?)null);

        var session = await driver.ConnectAsync("COM3", 115200, CancellationToken.None);

        session.DeviceSerial.Should().BeEmpty();
    }

    // ─── ConnectAsync: port operations ──────────

    [Fact]
    public async Task ConnectAsync_Success_OpensPort()
    {
        mockTelemetry
            .Setup(t => t.SendHeartbeatAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        mockTelemetry
            .Setup(t => t.WaitForHeartbeatAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        mockPortScanner
            .Setup(s => s.GetPortDescription(It.IsAny<string>()))
            .Returns((PortDescription?)null);

        await driver.ConnectAsync("COM9", 115200, CancellationToken.None);

        mockPort.Verify(p => p.Open("COM9", 115200), Times.Once);
    }

    [Fact]
    public async Task ConnectAsync_HeartbeatFails_SyncFails_ClosesPortAndThrows()
    {
        mockTelemetry
            .Setup(t => t.SendHeartbeatAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DeviceConnectionException("No heartbeat"));

        mockTelemetry
            .Setup(t => t.WaitForHeartbeatAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        mockBootloader
            .Setup(b => b.SyncAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var act = () => driver.ConnectAsync("COM9", 115200, CancellationToken.None);

        await act.Should().ThrowAsync<DeviceConnectionException>();
        mockPort.Verify(p => p.Close(), Times.Once);
    }
}
