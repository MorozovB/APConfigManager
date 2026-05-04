using APConfigManager.Api.Controllers;
using APConfigManager.Api.Dto;
using APConfigManager.Api.Hubs;
using APConfigManager.Core.Exceptions;
using APConfigManager.Core.Interfaces.Services;
using APConfigManager.Core.Models;
using APConfigManager.Core.Results;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace APConfigManager.Api.Tests.Controllers;

public class ParamsControllerTests
{
    private readonly Mock<IParamService> mockParamService;
    private readonly Mock<IHubContext<DeviceHub>> mockHubContext;
    private readonly Mock<IClientProxy> mockClientProxy;
    private readonly ParamsController controller;

    private readonly Guid sessionId = Guid.NewGuid();
    private readonly List<Parameter> testParams;

    public ParamsControllerTests()
    {
        mockParamService = new Mock<IParamService>();
        mockHubContext = new Mock<IHubContext<DeviceHub>>();
        mockClientProxy = new Mock<IClientProxy>();

        var mockClients = new Mock<IHubClients>();
        mockClients
            .Setup(c => c.Group(It.IsAny<string>()))
            .Returns(mockClientProxy.Object);
        mockHubContext
            .Setup(h => h.Clients)
            .Returns(mockClients.Object);

        testParams = new List<Parameter>
        {
            new() { Name = "ARMING_CHECK", Value = 1 },
            new() { Name = "BATT_MONITOR", Value = 4 },
            new() { Name = "SERVO1_MIN", Value = 1100 }
        };

        controller = new ParamsController(
            mockParamService.Object,
            mockHubContext.Object);
    }

    private static IFormFile CreateMockFile(string name = "params.param", long length = 512)
    {
        var stream = new MemoryStream(new byte[length]);
        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns(name);
        file.Setup(f => f.Length).Returns(length);
        file.Setup(f => f.OpenReadStream()).Returns(stream);
        return file.Object;
    }

    [Fact]
    public async Task Upload_ValidFile_ReturnsOkWithResult()
    {
        mockParamService
            .Setup(s => s.UploadAsync(
                It.IsAny<Guid>(),
                It.IsAny<Stream>(),
                It.IsAny<IProgress<(int, int)>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParameterUploadResult
            {
                Success = true,
                Sent = 3,
                Failed = 0,
                Total = 3
            });

        var file = CreateMockFile();

        var result = await controller.Upload(sessionId, file, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<OperationResultResponse>().Subject;
        response.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Upload_ValidFile_SendsOperationCompleted()
    {
        mockParamService
            .Setup(s => s.UploadAsync(
                It.IsAny<Guid>(),
                It.IsAny<Stream>(),
                It.IsAny<IProgress<(int, int)>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParameterUploadResult
            {
                Success = true,
                Sent = 3,
                Failed = 0,
                Total = 3
            });

        var file = CreateMockFile();

        await controller.Upload(sessionId, file, CancellationToken.None);

        mockClientProxy.Verify(
            c => c.SendCoreAsync(
                "OperationCompleted",
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Upload_NullFile_ReturnsBadRequest()
    {
        var result = await controller.Upload(sessionId, null!, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Upload_EmptyFile_ReturnsBadRequest()
    {
        var file = CreateMockFile(length: 0);

        var result = await controller.Upload(sessionId, file, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Upload_SessionNotFound_ReturnsNotFound()
    {
        mockParamService
            .Setup(s => s.UploadAsync(
                It.IsAny<Guid>(),
                It.IsAny<Stream>(),
                It.IsAny<IProgress<(int, int)>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SessionException("Session not found"));

        var file = CreateMockFile();

        var result = await controller.Upload(sessionId, file, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Upload_PartialFailure_ReturnsOkWithFailure()
    {
        mockParamService
            .Setup(s => s.UploadAsync(
                It.IsAny<Guid>(),
                It.IsAny<Stream>(),
                It.IsAny<IProgress<(int, int)>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParameterUploadResult
            {
                Success = false,
                Sent = 1,
                Failed = 2,
                Total = 3,
                ErrorMessage = "Read-only parameters"
            });

        var file = CreateMockFile();

        var result = await controller.Upload(sessionId, file, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<OperationResultResponse>().Subject;
        response.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Download_ValidSession_ReturnsOkWithParameters()
    {
        mockParamService
            .Setup(s => s.DownloadAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testParams);

        var result = await controller.Download(sessionId, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var parameters = okResult.Value.Should().BeAssignableTo<List<Parameter>>().Subject;
        parameters.Should().HaveCount(3);
        parameters[0].Name.Should().Be("ARMING_CHECK");
    }

    [Fact]
    public async Task Download_SessionNotFound_ReturnsNotFound()
    {
        mockParamService
            .Setup(s => s.DownloadAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SessionException("Session not found"));

        var result = await controller.Download(sessionId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Reset_ValidSession_ReturnsOkWithSuccess()
    {
        mockParamService
            .Setup(s => s.ResetAsync(sessionId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await controller.Reset(sessionId, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<OperationResultResponse>().Subject;
        response.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Reset_SessionNotFound_ReturnsNotFound()
    {
        mockParamService
            .Setup(s => s.ResetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SessionException("Session not found"));

        var result = await controller.Reset(sessionId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }
}
