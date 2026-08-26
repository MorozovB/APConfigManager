using APConfigManager.Api.Controllers;
using APConfigManager.Api.Dto;
using APConfigManager.Api.Hubs;
using APConfigManager.Core.Exceptions;
using APConfigManager.Core.Interfaces.Services;
using APConfigManager.Core.Results;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace APConfigManager.Api.Tests.Controllers;

public class EraseControllerTests
{
    private readonly Mock<IEraseService> mockEraseService;
    private readonly Mock<IHubContext<DeviceHub>> mockHubContext;
    private readonly Mock<IClientProxy> mockClientProxy;
    private readonly EraseController controller;

    private readonly Guid sessionId = Guid.NewGuid();

    public EraseControllerTests()
    {
        mockEraseService = new Mock<IEraseService>();
        mockHubContext = new Mock<IHubContext<DeviceHub>>();
        mockClientProxy = new Mock<IClientProxy>();

        var mockClients = new Mock<IHubClients>();
        mockClients
            .Setup(c => c.Group(It.IsAny<string>()))
            .Returns(mockClientProxy.Object);
        mockHubContext
            .Setup(h => h.Clients)
            .Returns(mockClients.Object);

        mockEraseService
            .Setup(s => s.EraseAsync(
                It.IsAny<Guid>(),
                It.IsAny<IProgress<(int, string)>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EraseResult { Success = true });

        //controller = new EraseController(
        //    mockEraseService.Object,
        //    mockHubContext.Object);
    }

    [Fact]
    public async Task Erase_ValidSession_ReturnsOkWithSuccess()
    {
        var result = await controller.Erase(sessionId, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<OperationResultResponse>().Subject;
        response.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Erase_ValidSession_DelegatesToService()
    {
        await controller.Erase(sessionId, CancellationToken.None);

        mockEraseService.Verify(
            s => s.EraseAsync(
                sessionId,
                It.IsAny<IProgress<(int, string)>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Erase_ValidSession_SendsOperationCompleted()
    {
        await controller.Erase(sessionId, CancellationToken.None);

        mockClientProxy.Verify(
            c => c.SendCoreAsync(
                "OperationCompleted",
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Erase_SessionNotFound_ReturnsNotFound()
    {
        mockEraseService
            .Setup(s => s.EraseAsync(
                It.IsAny<Guid>(),
                It.IsAny<IProgress<(int, string)>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SessionException("Session not found"));

        var result = await controller.Erase(sessionId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Erase_ServiceReturnsFailure_ReturnsOkWithFailure()
    {
        mockEraseService
            .Setup(s => s.EraseAsync(
                It.IsAny<Guid>(),
                It.IsAny<IProgress<(int, string)>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EraseResult
            {
                Success = false,
                ErrorMessage = "Erase timeout"
            });

        var result = await controller.Erase(sessionId, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<OperationResultResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Contain("timeout");
    }
}
