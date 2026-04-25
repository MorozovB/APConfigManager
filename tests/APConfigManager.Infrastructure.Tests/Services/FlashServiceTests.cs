using APConfigManager.Core.Enums;
using APConfigManager.Core.Exceptions;
using APConfigManager.Core.Interfaces.Drivers;
using APConfigManager.Core.Interfaces.Parsers;
using APConfigManager.Core.Interfaces.Services;
using APConfigManager.Core.Models;
using APConfigManager.Core.Results;
using APConfigManager.Infrastructure.Services;
using FluentAssertions;
using Moq;

namespace APConfigManager.Infrastructure.Tests.Services;

public class FlashServiceTests
{
    private readonly Mock<ISessionManager> mockSessionManager;
    private readonly Mock<IFirmwareParser> mockFirmwareParser;
    private readonly Mock<IFirmwareValidator> mockFirmwareValidator;
    private readonly Mock<IAutopilotDriver> mockDriver;
    private readonly FlashService flashService;

    private readonly Guid sessionId = Guid.NewGuid();
    private readonly DeviceSession session;
    private readonly FirmwarePackage firmware;
    private readonly DeviceInfo deviceInfo;

    public FlashServiceTests()
    {
        mockSessionManager = new Mock<ISessionManager>();
        mockFirmwareParser = new Mock<IFirmwareParser>();
        mockFirmwareValidator = new Mock<IFirmwareValidator>();
        mockDriver = new Mock<IAutopilotDriver>();

        session = new DeviceSession
        {
            Id = sessionId,
            Port = "COM3",
            BaudRate = 115200,
            State = DeviceState.Connected,
            ConnectedAt = DateTime.UtcNow
        };

        firmware = new FirmwarePackage
        {
            BoardId = 140,
            Magic = "APJ_MAGIC",
            Version = "4.5.1",
            GitIdentity = "abc123def456",
            ImageBytes = new byte[1024]
        };

        deviceInfo = new DeviceInfo
        {
            BoardId = 140,
            BoardRevision = 1,
            FlashSize = 2_097_152,
            BootloaderRevision = 5
        };

        mockSessionManager
            .Setup(s => s.GetSession(sessionId))
            .Returns(session);

        mockSessionManager
            .Setup(s => s.GetDriver(sessionId))
            .Returns(mockDriver.Object);

        mockFirmwareParser
            .Setup(p => p.Parse(It.IsAny<Stream>()))
            .Returns(firmware);

        mockDriver
            .Setup(d => d.GetFirmwareVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("different_version");

        mockDriver
            .Setup(d => d.GetDeviceInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(deviceInfo);

        mockFirmwareValidator
            .Setup(v => v.Validate(It.IsAny<FirmwarePackage>(), It.IsAny<DeviceInfo>()))
            .Returns(new ValidationResult { IsValid = true });

        mockDriver
            .Setup(d => d.FlashAsync(
                It.IsAny<FirmwarePackage>(),
                It.IsAny<IProgress<(int, string)>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FlashResult
            {
                Success = true,
                BytesWritten = 1024,
                FirmwareVersion = "4.5.1"
            });

        flashService = new FlashService(
            mockSessionManager.Object,
            mockFirmwareParser.Object,
            mockFirmwareValidator.Object);
    }

    private static IProgress<(int, string)> CreateProgress()
    {
        return new Progress<(int percent, string message)>();
    }

    [Fact]
    public async Task FlashAsyncsessionNotFound_ThrowsSessionException()
    {
        mockSessionManager
            .Setup(s => s.GetSession(sessionId))
            .Returns((DeviceSession?)null);

        var act = () => flashService.FlashAsync(
            sessionId,
            new MemoryStream(),
            CreateProgress(),
            CancellationToken.None);

        await act.Should().ThrowAsync<SessionException>();
    }

    [Fact]
    public async Task FlashAsync_SameVersion_ReturnsWasSameVersion()
    {
        mockDriver
            .Setup(d => d.GetFirmwareVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("abc123def456");

        var result = await flashService.FlashAsync(
            sessionId,
            new MemoryStream(),
            CreateProgress(),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.WasSameVersion.Should().BeTrue();

        mockDriver.Verify(
            d => d.FlashAsync(
                It.IsAny<FirmwarePackage>(),
                It.IsAny<IProgress<(int, string)>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task FlashAsync_EmptyCurrentVersion_ProceedsWithFlash()
    {
        mockDriver
            .Setup(d => d.GetFirmwareVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);

        var result = await flashService.FlashAsync(
            sessionId,
            new MemoryStream(),
            CreateProgress(),
            CancellationToken.None);

        result.Success.Should().BeTrue();

        mockDriver.Verify(
            d => d.FlashAsync(
                It.IsAny<FirmwarePackage>(),
                It.IsAny<IProgress<(int, string)>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task FlashAsync_InvalidFirmware_ReturnsFailure()
    {
        mockFirmwareValidator
            .Setup(v => v.Validate(It.IsAny<FirmwarePackage>(), It.IsAny<DeviceInfo>()))
            .Returns(new ValidationResult
            {
                IsValid = false,
                ErrorMessage = "Board ID mismatch"
            });

        var result = await flashService.FlashAsync(
            sessionId,
            new MemoryStream(),
            CreateProgress(),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("mismatch");

        mockDriver.Verify(
            d => d.FlashAsync(
                It.IsAny<FirmwarePackage>(),
                It.IsAny<IProgress<(int, string)>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task FlashAsync_ValidFirmware_DelegatesToDriver()
    {
        var result = await flashService.FlashAsync(
            sessionId,
            new MemoryStream(),
            CreateProgress(),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.BytesWritten.Should().Be(1024);
        result.FirmwareVersion.Should().Be("4.5.1");

        mockDriver.Verify(
            d => d.FlashAsync(
                It.IsAny<FirmwarePackage>(),
                It.IsAny<IProgress<(int, string)>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task FlashAsync_ValidFirmware_CallsParserFirst()
    {
        await flashService.FlashAsync(
            sessionId,
            new MemoryStream(),
            CreateProgress(),
            CancellationToken.None);

        mockFirmwareParser.Verify(
            p => p.Parse(It.IsAny<Stream>()),
            Times.Once);
    }

    [Fact]
    public async Task FlashAsync_ValidFirmware_CallsValidatorBeforeFlash()
    {
        await flashService.FlashAsync(
            sessionId,
            new MemoryStream(),
            CreateProgress(),
            CancellationToken.None);

        mockFirmwareValidator.Verify(
            v => v.Validate(firmware, deviceInfo),
            Times.Once);
    }

    [Fact]
    public async Task FlashAsync_DriverReturnsFailure_ReturnsFailure()
    {
        mockDriver
            .Setup(d => d.FlashAsync(
                It.IsAny<FirmwarePackage>(),
                It.IsAny<IProgress<(int, string)>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FlashResult
            {
                Success = false,
                ErrorMessage = "CRC verification failed."
            });

        var result = await flashService.FlashAsync(
            sessionId,
            new MemoryStream(),
            CreateProgress(),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("CRC");
    }

    [Fact]
    public async Task FlashAsync_NullStream_ThrowsArgumentNullException()
    {
        var act = () => flashService.FlashAsync(
            sessionId,
            null!,
            CreateProgress(),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task FlashAsync_NullProgress_ThrowsArgumentNullException()
    {
        var act = () => flashService.FlashAsync(
            sessionId,
            new MemoryStream(),
            null!,
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
