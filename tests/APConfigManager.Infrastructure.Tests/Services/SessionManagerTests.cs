using APConfigManager.Core.Enums;
using APConfigManager.Core.Exceptions;
using APConfigManager.Core.Interfaces.Drivers;
using APConfigManager.Core.Models;
using APConfigManager.Infrastructure.Services;
using FluentAssertions;
using Moq;

namespace APConfigManager.Infrastructure.Tests.Services;

public class SessionManagerTests : IAsyncDisposable
{
    private readonly Mock<IAutopilotDriver> mockDriver;
    private readonly SessionManager sessionManager;
    private int portCounter;

    public SessionManagerTests()
    {
        mockDriver = new Mock<IAutopilotDriver>();
        portCounter = 0;

        mockDriver
            .Setup(d => d.ConnectAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns((string port, int baudRate, CancellationToken _) =>
                Task.FromResult(new DeviceSession
                {
                    Id = Guid.NewGuid(),
                    Port = port,
                    BaudRate = baudRate,
                    State = DeviceState.Connected,
                    ConnectedAt = DateTime.UtcNow
                }));

        mockDriver
            .Setup(d => d.DisconnectAsync())
            .Returns(Task.CompletedTask);

        // sessionManager = new SessionManager(() => mockDriver.Object);
    }

    public async ValueTask DisposeAsync()
    {
        await sessionManager.DisposeAsync();
    }

    private string NextPort() => $"COM{++portCounter}";

    [Fact]
    public async Task CreateSession_ValidPort_ReturnsSession()
    {
        var session = await sessionManager.CreateSessionAsync("COM3", 115200, CancellationToken.None);

        session.Should().NotBeNull();
        session.Port.Should().Be("COM3");
        session.BaudRate.Should().Be(115200);
        session.State.Should().Be(DeviceState.Connected);
        sessionManager.GetAllSessions().Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateSession_PortAlreadyInUse_ThrowsSessionException()
    {
        await sessionManager.CreateSessionAsync("COM3", 115200, CancellationToken.None);

        var act = () => sessionManager.CreateSessionAsync("COM3", 115200, CancellationToken.None);

        await act.Should().ThrowAsync<SessionException>();
    }

    [Fact]
    public async Task CreateSession_SamePortDifferentCase_ThrowsSessionException()
    {
        await sessionManager.CreateSessionAsync("COM3", 115200, CancellationToken.None);

        var act = () => sessionManager.CreateSessionAsync("com3", 115200, CancellationToken.None);

        await act.Should().ThrowAsync<SessionException>();
    }

    [Fact]
    public async Task CreateSession_MaxSessionsReached_ThrowsSessionException()
    {
        for (var i = 0; i < 4; i++)
        {
            await sessionManager.CreateSessionAsync(NextPort(), 115200, CancellationToken.None);
        }

        var act = () => sessionManager.CreateSessionAsync(NextPort(), 115200, CancellationToken.None);

        await act.Should().ThrowAsync<SessionException>();
    }

    [Fact]
    public async Task CreateSession_FourDifferentPorts_AllSucceed()
    {
        for (var i = 0; i < 4; i++)
        {
            await sessionManager.CreateSessionAsync(NextPort(), 115200, CancellationToken.None);
        }

        sessionManager.GetAllSessions().Should().HaveCount(4);
    }

    [Fact]
    public async Task GetSession_ExistingId_ReturnsSession()
    {
        var created = await sessionManager.CreateSessionAsync("COM3", 115200, CancellationToken.None);

        var found = sessionManager.GetSession(created.Id);

        found.Should().NotBeNull();
        found!.Port.Should().Be("COM3");
    }

    [Fact]
    public void GetSession_NonExistingId_ReturnsNull()
    {
        var found = sessionManager.GetSession(Guid.NewGuid());

        found.Should().BeNull();
    }

    [Fact]
    public async Task GetDriver_ExistingSession_ReturnsDriver()
    {
        var session = await sessionManager.CreateSessionAsync("COM3", 115200, CancellationToken.None);

        var driver = sessionManager.GetDriver(session.Id);

        driver.Should().NotBeNull();
    }

    [Fact]
    public void GetDriver_NonExistingId_ThrowsSessionException()
    {
        var act = () => sessionManager.GetDriver(Guid.NewGuid());

        act.Should().Throw<SessionException>();
    }

    [Fact]
    public async Task CloseSession_ExistingSession_RemovesFromList()
    {
        var session = await sessionManager.CreateSessionAsync("COM3", 115200, CancellationToken.None);

        await sessionManager.CloseSessionAsync(session.Id);

        sessionManager.GetAllSessions().Should().BeEmpty();
        sessionManager.GetSession(session.Id).Should().BeNull();
        mockDriver.Verify(d => d.DisconnectAsync(), Times.Once);
    }

    [Fact]
    public async Task CloseSession_NonExistingId_ThrowsSessionException()
    {
        var act = () => sessionManager.CloseSessionAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<SessionException>();
    }

    [Fact]
    public async Task CloseSession_ThenCreateOnSamePort_Succeeds()
    {
        var session = await sessionManager.CreateSessionAsync("COM3", 115200, CancellationToken.None);
        await sessionManager.CloseSessionAsync(session.Id);

        var newSession = await sessionManager.CreateSessionAsync("COM3", 115200, CancellationToken.None);

        newSession.Should().NotBeNull();
        newSession.Port.Should().Be("COM3");
        sessionManager.GetAllSessions().Should().HaveCount(1);
    }

    [Fact]
    public void GetAllSessions_NoSessions_ReturnsEmptyList()
    {
        var sessions = sessionManager.GetAllSessions();

        sessions.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllSessions_MultipleSessions_ReturnsAll()
    {
        await sessionManager.CreateSessionAsync("COM3", 115200, CancellationToken.None);
        await sessionManager.CreateSessionAsync("COM4", 115200, CancellationToken.None);
        await sessionManager.CreateSessionAsync("COM5", 115200, CancellationToken.None);

        var sessions = sessionManager.GetAllSessions();

        sessions.Should().HaveCount(3);
    }

    //[Fact]
    //public void DriverFactory_ReturnsNotNull()
    //{
    //    var driver = new Mock<IAutopilotDriver>();
    //    var manager = new SessionManager(() => driver.Object);

    //    var field = typeof(SessionManager).GetField("driverFactory",
    //        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

    //    var factory = field!.GetValue(manager) as Func<IAutopilotDriver>;

    //    factory.Should().NotBeNull();
    //    factory!().Should().NotBeNull();
    //}

    [Fact]
    public void SimpleFactory_Test()
    {
        Func<IAutopilotDriver> factory = () => new Mock<IAutopilotDriver>().Object;
        var result = factory();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Mock_ConnectAsync_ReturnsNotNull()
    {
        var result = await mockDriver.Object.ConnectAsync("COM3", 115200, CancellationToken.None);
        result.Should().NotBeNull();
    }
}
