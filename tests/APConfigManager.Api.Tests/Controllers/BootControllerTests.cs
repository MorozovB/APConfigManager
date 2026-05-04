using APConfigManager.Api.Controllers;
using APConfigManager.Api.Dto;
using APConfigManager.Api.Hubs;
using APConfigManager.Core.Enums;
using APConfigManager.Core.Exceptions;
using APConfigManager.Core.Interfaces.Drivers;
using APConfigManager.Core.Interfaces.Services;
using APConfigManager.Core.Models;
using APConfigManager.Core.Results;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace APConfigManager.Api.Tests.Controllers;

public class BootControllerTests
{
    private readonly Mock<ISessionManager> mockSessionManager;
    private readonly Mock<IHubContext<DeviceHub>> mockHubContext;
    private readonly Mock<IClientProxy> mockClientProxy;
    private readonly Mock<IAutopilotDriver> mockDriver;
    private readonly BootController controller;

    private readonly Guid sessionId = Guid.NewGuid();
    private readonly DeviceSession session;

    public BootControllerTests()
    {
        mockSessionManager = new Mock<ISessionManager>();
        mockHubContext = new Mock<IHubContext<DeviceHub>>();
        mockClientProxy = new Mock<IClientProxy>();
        mockDriver = new Mock<IAutopilotDriver>();

        var mockClients = new Mock<IHubClients>();
        mockClients
            .Setup(c => c.Group(It.IsAny<string>()))
            .Returns(mockClientProxy.Object);
        mockHubContext
            .Setup(h => h.Clients)
            .Returns(mockClients.Object);

        session = new DeviceSession
        {
            Id = sessionId,
            Port = "COM3",
            BaudRate = 115200,
            State = DeviceState.InBootloader,
            ConnectedAt = DateTime.UtcNow
        };

        mockSessionManager
            .Setup(s => s.GetSession(sessionId))
            .Returns(session);

        mockSessionManager
            .Setup(s => s.GetDriver(sessionId))
            .Returns(mockDriver.Object);

        mockDriver
            .Setup(d => d.RebootAsync(BootMode.Normal, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BootResult
            {
                Success = true,
                NewPort = "COM3"
            });

        controller = new BootController(
            mockSessionManager.Object,
            mockHubContext.Object);
    }

    [Fact]
    public async Task Boot_ValidSession_ReturnsOkWithSuccess()
    {
        var result = await controller.Boot(sessionId, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<OperationResultResponse>().Subject;
        response.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Boot_ValidSession_CallsRebootNormal()
    {
        await controller.Boot(sessionId, CancellationToken.None);

        mockDriver.Verify(
            d => d.RebootAsync(BootMode.Normal, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Boot_SuccessfulBoot_SendsStateChangedNotification()
    {
        await controller.Boot(sessionId, CancellationToken.None);

        mockClientProxy.Verify(
            c => c.SendCoreAsync(
                "DeviceStateChanged",
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Boot_SessionNotFound_ReturnsNotFound()
    {
        mockSessionManager
            .Setup(s => s.GetSession(It.IsAny<Guid>()))
            .Returns((DeviceSession?)null);

        var result = await controller.Boot(Guid.NewGuid(), CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Boot_DriverReturnsFailure_ReturnsOkWithFailure()
    {
        mockDriver
            .Setup(d => d.RebootAsync(BootMode.Normal, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BootResult
            {
                Success = false,
                ErrorMessage = "Boot failed"
            });

        var result = await controller.Boot(sessionId, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<OperationResultResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Contain("Boot failed");
    }

    [Fact]
    public async Task Boot_DriverReturnsFailure_DoesNotSendStateChanged()
    {
        mockDriver
            .Setup(d => d.RebootAsync(BootMode.Normal, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BootResult
            {
                Success = false,
                ErrorMessage = "Boot failed"
            });

        await controller.Boot(sessionId, CancellationToken.None);

        mockClientProxy.Verify(
            c => c.SendCoreAsync(
                "DeviceStateChanged",
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Boot_DriverThrowsException_ReturnsServerError()
    {
        mockDriver
            .Setup(d => d.RebootAsync(BootMode.Normal, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DeviceConnectionException("Connection lost"));

        var result = await controller.Boot(sessionId, CancellationToken.None);

        var statusResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(500);
    }
}
