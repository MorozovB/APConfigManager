using APConfigManager.Api.Controllers;
using APConfigManager.Api.Dto;
using APConfigManager.Core.Interfaces.Transport;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace APConfigManager.Api.Tests.Controllers;

public class PortsControllerTests
{
    private readonly Mock<IPortScanner> mockPortScanner;
    private readonly PortsController controller;

    public PortsControllerTests()
    {
        mockPortScanner = new Mock<IPortScanner>();
        controller = new PortsController(mockPortScanner.Object);
    }

    [Fact]
    public void GetPorts_HasPorts_ReturnsOkWithPortList()
    {
        mockPortScanner
            .Setup(s => s.GetAvailablePorts())
            .Returns(new List<string> { "COM3", "COM5", "COM7" });

        var result = controller.GetPorts();

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var ports = okResult.Value.Should().BeAssignableTo<List<PortInfo>>().Subject;
        ports.Should().HaveCount(3);
        ports[0].Name.Should().Be("COM3");
        ports[1].Name.Should().Be("COM5");
        ports[2].Name.Should().Be("COM7");
    }

    [Fact]
    public void GetPorts_NoPorts_ReturnsOkWithEmptyList()
    {
        mockPortScanner
            .Setup(s => s.GetAvailablePorts())
            .Returns(new List<string>());

        var result = controller.GetPorts();

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var ports = okResult.Value.Should().BeAssignableTo<List<PortInfo>>().Subject;
        ports.Should().BeEmpty();
    }
}
