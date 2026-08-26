using APConfigManager.Core.Enums;
using APConfigManager.Core.Exceptions;
using APConfigManager.Core.Interfaces.Drivers;
using APConfigManager.Core.Interfaces.Services;
using APConfigManager.Core.Models;
using APConfigManager.Core.Results;
using APConfigManager.Infrastructure.Services;
using FluentAssertions;
using Moq;

namespace APConfigManager.Infrastructure.Tests.Services;

public class EraseServiceTests
{
    private readonly Mock<ISessionManager> mockSessionManager;
    private readonly Mock<IAutopilotDriver> mockDriver;
    private readonly EraseService eraseService;

    private readonly Guid sessionId = Guid.NewGuid();
    private readonly DeviceSession session;

    public EraseServiceTests()
    {
        mockSessionManager = new Mock<ISessionManager>();
        mockDriver = new Mock<IAutopilotDriver>();

        session = new DeviceSession
        {
            Id = sessionId,
            Port = "COM3",
            BaudRate = 115200,
            State = DeviceState.Connected,
            ConnectedAt = DateTime.UtcNow
        };

        mockSessionManager
            .Setup(s => s.GetSession(sessionId))
            .Returns(session);

        mockSessionManager
            .Setup(s => s.GetDriver(sessionId))
            .Returns(mockDriver.Object);

        mockDriver
            .Setup(d => d.EraseAsync(
                It.IsAny<IProgress<(int, string)>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EraseResult { Success = true });

        // eraseService = new EraseService(mockSessionManager.Object);
    }

    private static IProgress<(int, string)> CreateProgress()
    {
        return new Progress<(int percent, string message)>();
    }
    [Fact]
    public async Task EraseAsync_SessionNotFound_ThrowsSessionException()
    {
        mockSessionManager
            .Setup(s => s.GetSession(sessionId))
            .Returns((DeviceSession?)null);

        var act = () => eraseService.EraseAsync(
            sessionId,
            CreateProgress(),
            CancellationToken.None);

        await act.Should().ThrowAsync<SessionException>();
    }

    [Fact]
    public async Task EraseAsync_ValidSession_ReturnsSuccess()
    {
        var result = await eraseService.EraseAsync(
            sessionId,
            CreateProgress(),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task EraseAsync_ValidSession_DelegatesToDriver()
    {
        await eraseService.EraseAsync(
            sessionId,
            CreateProgress(),
            CancellationToken.None);

        mockDriver.Verify(
            d => d.EraseAsync(
                It.IsAny<IProgress<(int, string)>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EraseAsync_DriverReturnsFailure_ReturnsFailure()
    {
        mockDriver
            .Setup(d => d.EraseAsync(
                It.IsAny<IProgress<(int, string)>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EraseResult
            {
                Success = false,
                ErrorMessage = "Erase timeout."
            });

        var result = await eraseService.EraseAsync(
            sessionId,
            CreateProgress(),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("timeout");
    }

    [Fact]
    public async Task EraseAsync_DriverThrowsException_PropagatesException()
    {
        mockDriver
            .Setup(d => d.EraseAsync(
                It.IsAny<IProgress<(int, string)>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BootloaderException("Connection lost."));

        var act = () => eraseService.EraseAsync(
            sessionId,
            CreateProgress(),
            CancellationToken.None);

        await act.Should().ThrowAsync<BootloaderException>();
    }
}
