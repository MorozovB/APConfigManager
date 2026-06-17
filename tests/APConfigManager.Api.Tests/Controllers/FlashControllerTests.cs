using APConfigManager.Api.Controllers;
using APConfigManager.Api.Dto;
using APConfigManager.Api.Hubs;
using APConfigManager.Core.Exceptions;
using APConfigManager.Core.Interfaces.Services;
using APConfigManager.Core.Results;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace APConfigManager.Api.Tests.Controllers;

public class FlashControllerTests
{
    private readonly Mock<IFlashService> mockFlashService;
    private readonly Mock<IHubContext<DeviceHub>> mockHubContext;
    private readonly Mock<IClientProxy> mockClientProxy;
    private readonly Mock<ISessionManager> mockSessionManager;
    private readonly FlashController controller;

    private readonly Guid sessionId = Guid.NewGuid();

    public FlashControllerTests()
    {
        mockFlashService = new Mock<IFlashService>();
        mockHubContext = new Mock<IHubContext<DeviceHub>>();
        mockClientProxy = new Mock<IClientProxy>();

        var mockClients = new Mock<IHubClients>();
        mockClients
            .Setup(c => c.Group(It.IsAny<string>()))
            .Returns(mockClientProxy.Object);
        mockHubContext
            .Setup(h => h.Clients)
            .Returns(mockClients.Object);

        mockFlashService
            .Setup(s => s.FlashAsync(
                It.IsAny<Guid>(),
                It.IsAny<Stream>(),
                It.IsAny<IProgress<(int, string)>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FlashResult
            {
                Success = true,
                BytesWritten = 1024,
                FirmwareVersion = "4.5.1"
            });

        controller = new FlashController(
            mockFlashService.Object,
            mockSessionManager.Object,
            mockHubContext.Object);
    }

    private static IFormFile CreateMockFile(string name = "firmware.apj", long length = 1024)
    {
        var stream = new MemoryStream(new byte[length]);
        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns(name);
        file.Setup(f => f.Length).Returns(length);
        file.Setup(f => f.OpenReadStream()).Returns(stream);
        return file.Object;
    }

    [Fact]
    public async Task Flash_ValidFile_ReturnsOkWithResult()
    {
        var file = CreateMockFile();

        var result = await controller.Flash(sessionId, file, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<OperationResultResponse>().Subject;
        response.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Flash_ValidFile_DelegatesToService()
    {
        var file = CreateMockFile();

        await controller.Flash(sessionId, file, CancellationToken.None);

        mockFlashService.Verify(
            s => s.FlashAsync(
                sessionId,
                It.IsAny<Stream>(),
                It.IsAny<IProgress<(int, string)>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Flash_ValidFile_SendsOperationCompleted()
    {
        var file = CreateMockFile();

        await controller.Flash(sessionId, file, CancellationToken.None);

        mockClientProxy.Verify(
            c => c.SendCoreAsync(
                "OperationCompleted",
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Flash_NullFile_ReturnsBadRequest()
    {
        var result = await controller.Flash(sessionId, null!, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Flash_EmptyFile_ReturnsBadRequest()
    {
        var file = CreateMockFile(length: 0);

        var result = await controller.Flash(sessionId, file, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Flash_SessionNotFound_ReturnsNotFound()
    {
        mockFlashService
            .Setup(s => s.FlashAsync(
                It.IsAny<Guid>(),
                It.IsAny<Stream>(),
                It.IsAny<IProgress<(int, string)>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SessionException("Session not found"));

        var file = CreateMockFile();

        var result = await controller.Flash(sessionId, file, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Flash_ServiceReturnsFailure_ReturnsOkWithFailure()
    {
        mockFlashService
            .Setup(s => s.FlashAsync(
                It.IsAny<Guid>(),
                It.IsAny<Stream>(),
                It.IsAny<IProgress<(int, string)>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FlashResult
            {
                Success = false,
                ErrorMessage = "CRC verification failed"
            });

        var file = CreateMockFile();

        var result = await controller.Flash(sessionId, file, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<OperationResultResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Contain("CRC");
    }
}
