using APConfigManager.Api.Controllers;
using APConfigManager.Api.Dto;
using APConfigManager.Api.Hubs;
using APConfigManager.Core.Enums;
using APConfigManager.Core.Exceptions;
using APConfigManager.Core.Interfaces.Services;
using APConfigManager.Core.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace APConfigManager.Api.Tests.Controllers;

public class SessionsControllerTests
{
    private readonly Mock<ISessionManager> mockSessionManager;
    private readonly Mock<IHubContext<DeviceHub>> mockHubContext;
    private readonly Mock<IClientProxy> mockClientProxy;
    private readonly SessionsController controller;

    private readonly Guid sessionId = Guid.NewGuid();
    private readonly DeviceSession session;

    public SessionsControllerTests()
    {
        mockSessionManager = new Mock<ISessionManager>();
        mockHubContext = new Mock<IHubContext<DeviceHub>>();
        mockClientProxy = new Mock<IClientProxy>();

        // SignalR mock chain: hubContext.Clients.Group(id) → clientProxy
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
            State = DeviceState.Connected,
            ConnectedAt = DateTime.UtcNow,
            DeviceSerial = "AAA111"
        };

        controller = new SessionsController(
            mockSessionManager.Object,
            mockHubContext.Object);
    }

    [Fact]
    public async Task CreateSession_ValidRequest_ReturnsCreated()
    {
        mockSessionManager
            .Setup(s => s.CreateSessionAsync("COM3", 115200, It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(session));

        var request = new CreateSessionRequest { Port = "COM3", BaudRate = 115200 };

        var result = await controller.CreateSession(request, CancellationToken.None);

        var createdResult = result.Result.Should().BeOfType<CreatedResult>().Subject;
        var response = createdResult.Value.Should().BeOfType<SessionResponse>().Subject;
        response.Port.Should().Be("COM3");
        response.BaudRate.Should().Be(115200);
        response.State.Should().Be("Connected");
    }

    [Fact]
    public async Task CreateSession_EmptyPort_ReturnsBadRequest()
    {
        var request = new CreateSessionRequest { Port = "", BaudRate = 115200 };

        var result = await controller.CreateSession(request, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateSession_PortBusy_ReturnsConflict()
    {
        mockSessionManager
            .Setup(s => s.CreateSessionAsync("COM3", 115200, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SessionException("Port is already in use"));

        var request = new CreateSessionRequest { Port = "COM3", BaudRate = 115200 };

        var result = await controller.CreateSession(request, CancellationToken.None);

        result.Result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task CreateSession_DeviceNotResponding_ReturnsBadRequest()
    {
        mockSessionManager
            .Setup(s => s.CreateSessionAsync("COM3", 115200, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DeviceConnectionException("No response from device"));

        var request = new CreateSessionRequest { Port = "COM3", BaudRate = 115200 };

        var result = await controller.CreateSession(request, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateSession_ValidRequest_SendsSignalRNotification()
    {
        mockSessionManager
            .Setup(s => s.CreateSessionAsync("COM3", 115200, It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(session));

        var request = new CreateSessionRequest { Port = "COM3", BaudRate = 115200 };

        await controller.CreateSession(request, CancellationToken.None);

        mockClientProxy.Verify(
            c => c.SendCoreAsync(
                "DeviceStateChanged",
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void GetAllSessions_HasSessions_ReturnsOkWithList()
    {
        mockSessionManager
            .Setup(s => s.GetAllSessions())
            .Returns(new List<DeviceSession> { session });

        var result = controller.GetAllSessions();

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var sessions = okResult.Value.Should().BeAssignableTo<List<SessionResponse>>().Subject;
        sessions.Should().ContainSingle();
        sessions[0].Port.Should().Be("COM3");
    }

    [Fact]
    public void GetAllSessions_NoSessions_ReturnsOkWithEmptyList()
    {
        mockSessionManager
            .Setup(s => s.GetAllSessions())
            .Returns(new List<DeviceSession>());

        var result = controller.GetAllSessions();

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var sessions = okResult.Value.Should().BeAssignableTo<List<SessionResponse>>().Subject;
        sessions.Should().BeEmpty();
    }

    [Fact]
    public void GetSession_ExistingId_ReturnsOk()
    {
        mockSessionManager
            .Setup(s => s.GetSession(sessionId))
            .Returns(session);

        var result = controller.GetSession(sessionId);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<SessionResponse>().Subject;
        response.Port.Should().Be("COM3");
    }

    [Fact]
    public void GetSession_NonExistingId_ReturnsNotFound()
    {
        mockSessionManager
            .Setup(s => s.GetSession(It.IsAny<Guid>()))
            .Returns((DeviceSession?)null);

        var result = controller.GetSession(Guid.NewGuid());

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task CloseSession_ExistingId_ReturnsNoContent()
    {
        mockSessionManager
            .Setup(s => s.CloseSessionAsync(sessionId))
            .Returns(Task.CompletedTask);

        var result = await controller.CloseSession(sessionId);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task CloseSession_NonExistingId_ReturnsNotFound()
    {
        mockSessionManager
            .Setup(s => s.CloseSessionAsync(It.IsAny<Guid>()))
            .ThrowsAsync(new SessionException("Session not found"));

        var result = await controller.CloseSession(Guid.NewGuid());

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task CloseSession_ExistingId_SendsSignalRNotification()
    {
        mockSessionManager
            .Setup(s => s.CloseSessionAsync(sessionId))
            .Returns(Task.CompletedTask);

        await controller.CloseSession(sessionId);

        mockClientProxy.Verify(
            c => c.SendCoreAsync(
                "DeviceStateChanged",
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateSession_ValidRequest_ResponseContainsDeviceSerial()
    {
        mockSessionManager
            .Setup(s => s.CreateSessionAsync("COM9", 115200, It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(session));

        var request = new CreateSessionRequest { Port = "COM9", BaudRate = 115200 };
        var result = await controller.CreateSession(request, CancellationToken.None);

        var createdResult = result.Result.Should().BeOfType<CreatedResult>().Subject;
        var response = createdResult.Value.Should().BeOfType<SessionResponse>().Subject;
        response.DeviceSerial.Should().Be("AAA111");
    }

}
