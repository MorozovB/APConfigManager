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

public class ParamServiceTests
{
    private readonly Mock<ISessionManager> mockSessionManager;
    private readonly Mock<IParamFileParser> mockParamParser;
    private readonly Mock<IAutopilotDriver> mockDriver;
    private readonly ParamService paramService;

    private readonly Guid sessionId = Guid.NewGuid();
    private readonly DeviceSession session;
    private readonly List<Parameter> testParams;

    public ParamServiceTests()
    {
        mockSessionManager = new Mock<ISessionManager>();
        mockParamParser = new Mock<IParamFileParser>();
        mockDriver = new Mock<IAutopilotDriver>();

        session = new DeviceSession
        {
            Id = sessionId,
            Port = "COM3",
            BaudRate = 115200,
            State = DeviceState.Connected,
            ConnectedAt = DateTime.UtcNow
        };

        testParams = new List<Parameter>
        {
            new() { Name = "ARMING_CHECK", Value = 1 },
            new() { Name = "BATT_MONITOR", Value = 4 },
            new() { Name = "SERVO1_MIN", Value = 1100 }
        };

        mockSessionManager
            .Setup(s => s.GetSession(sessionId))
            .Returns(session);

        mockSessionManager
            .Setup(s => s.GetDriver(sessionId))
            .Returns(mockDriver.Object);

        mockParamParser
            .Setup(p => p.Parse(It.IsAny<Stream>()))
            .Returns(testParams);

        mockDriver
            .Setup(d => d.WriteParamsAsync(
                It.IsAny<List<Parameter>>(),
                It.IsAny<IProgress<(int, int)>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParameterUploadResult
            {
                Success = true,
                Sent = 3,
                Failed = 0,
                Total = 3
            });

        mockDriver
            .Setup(d => d.ReadParamsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(testParams);

        //mockDriver
        //    .Setup(d => d.ResetParamsAsync(It.IsAny<CancellationToken>()))
        //    .Returns(Task.CompletedTask);

        //paramService = new ParamService(
        //    mockSessionManager.Object,
        //    mockParamParser.Object);
    }

    private static IProgress<(int, int)> CreateProgress()
    {
        return new Progress<(int current, int total)>();
    }

    [Fact]
    public async Task UploadAsync_SessionNotFound_ThrowsSessionException()
    {
        mockSessionManager
            .Setup(s => s.GetSession(sessionId))
            .Returns((DeviceSession?)null);

        var act = () => paramService.UploadAsync(
            sessionId,
            new MemoryStream(),
            CreateProgress(),
            CancellationToken.None);

        await act.Should().ThrowAsync<SessionException>();
    }

    [Fact]
    public async Task UploadAsync_ValidParams_ReturnsSuccess()
    {
        var result = await paramService.UploadAsync(
            sessionId,
            new MemoryStream(),
            CreateProgress(),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Sent.Should().Be(3);
        result.Failed.Should().Be(0);
        result.Total.Should().Be(3);
    }

    [Fact]
    public async Task UploadAsync_ValidParams_ParsesFileThenDelegatesToDriver()
    {
        await paramService.UploadAsync(
            sessionId,
            new MemoryStream(),
            CreateProgress(),
            CancellationToken.None);

        mockParamParser.Verify(
            p => p.Parse(It.IsAny<Stream>()),
            Times.Once);

        mockDriver.Verify(
            d => d.WriteParamsAsync(
                It.IsAny<List<Parameter>>(),
                It.IsAny<IProgress<(int, int)>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UploadAsync_DriverReturnsFailure_ReturnsFailure()
    {
        mockDriver
            .Setup(d => d.WriteParamsAsync(
                It.IsAny<List<Parameter>>(),
                It.IsAny<IProgress<(int, int)>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParameterUploadResult
            {
                Success = false,
                Sent = 1,
                Failed = 2,
                Total = 3,
                ErrorMessage = "Read-only parameters."
            });

        var result = await paramService.UploadAsync(
            sessionId,
            new MemoryStream(),
            CreateProgress(),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Sent.Should().Be(1);
        result.Failed.Should().Be(2);
        result.ErrorMessage.Should().Contain("Read-only");
    }

    [Fact]
    public async Task UploadAsync_NullStream_ThrowsArgumentNullException()
    {
        var act = () => paramService.UploadAsync(
            sessionId,
            null!,
            CreateProgress(),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task UploadAsync_NullProgress_ThrowsArgumentNullException()
    {
        var act = () => paramService.UploadAsync(
            sessionId,
            new MemoryStream(),
            null!,
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task DownloadAsync_SessionNotFound_ThrowsSessionException()
    {
        mockSessionManager
            .Setup(s => s.GetSession(sessionId))
            .Returns((DeviceSession?)null);

        var act = () => paramService.DownloadAsync(
            sessionId,
            CancellationToken.None);

        await act.Should().ThrowAsync<SessionException>();
    }

    [Fact]
    public async Task DownloadAsync_ValidSession_ReturnsParameters()
    {
        var result = await paramService.DownloadAsync(
            sessionId,
            CancellationToken.None);

        result.Should().HaveCount(3);
        result[0].Name.Should().Be("ARMING_CHECK");
        result[1].Name.Should().Be("BATT_MONITOR");
        result[2].Name.Should().Be("SERVO1_MIN");
    }

    [Fact]
    public async Task DownloadAsync_ValidSession_DelegatesToDriver()
    {
        await paramService.DownloadAsync(
            sessionId,
            CancellationToken.None);

        mockDriver.Verify(
            d => d.ReadParamsAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResetAsync_SessionNotFound_ThrowsSessionException()
    {
        mockSessionManager
            .Setup(s => s.GetSession(sessionId))
            .Returns((DeviceSession?)null);

        var act = () => paramService.ResetAsync(
            sessionId,
            CancellationToken.None);

        await act.Should().ThrowAsync<SessionException>();
    }

    [Fact]
    public async Task ResetAsync_ValidSession_DelegatesToDriver()
    {
        await paramService.ResetAsync(
            sessionId,
            CancellationToken.None);

        mockDriver.Verify(
            d => d.ResetParamsAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResetAsync_DriverThrowsException_PropagatesException()
    {
        mockDriver
            .Setup(d => d.ResetParamsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DeviceConnectionException("Connection lost."));

        var act = () => paramService.ResetAsync(
            sessionId,
            CancellationToken.None);

        await act.Should().ThrowAsync<DeviceConnectionException>();
    }
}
