using APConfigManager.Api.Controllers;
using APConfigManager.Api.Dto;
using APConfigManager.Core.Interfaces.Transport;
using APConfigManager.Core.Models;
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
    public void GetPorts_HasPorts_ReturnsOkWithPortListAndDescriptions()
    {
        mockPortScanner
            .Setup(s => s.GetAvailablePortsDetailed())
            .Returns(new List<PortDescription>
            {
                new() { Name = "COM9", Description = "Cube Orange+ Mavlink", DeviceSerial = "AAA111", IsMavlink = true },
                new() { Name = "COM10", Description = "Cube Orange+ SLCAN", DeviceSerial = "AAA111", IsMavlink = false },
                new() { Name = "COM5", Description = "Cube Black Mavlink", DeviceSerial = "BBB222", IsMavlink = true }
            });

        var result = controller.GetPorts();

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var ports = okResult.Value.Should().BeAssignableTo<List<PortInfo>>().Subject;
        ports.Should().HaveCount(3);
        ports[0].Name.Should().Be("COM9");
        ports[0].Description.Should().Be("Cube Orange+ Mavlink");
        ports[1].Name.Should().Be("COM10");
        ports[1].Description.Should().Be("Cube Orange+ SLCAN");
    }

    [Fact]
    public void GetPorts_NoPorts_ReturnsOkWithEmptyList()
    {
        mockPortScanner
            .Setup(s => s.GetAvailablePortsDetailed())
            .Returns(new List<PortDescription>());

        var result = controller.GetPorts();

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var ports = okResult.Value.Should().BeAssignableTo<List<PortInfo>>().Subject;
        ports.Should().BeEmpty();
    }

    [Fact]
    public void GetPorts_WmiFallback_ReturnsPortsWithoutDescription()
    {
        mockPortScanner
            .Setup(s => s.GetAvailablePortsDetailed())
            .Returns(new List<PortDescription>
            {
                new() { Name = "COM3", Description = "", DeviceSerial = "" }
            });

        var result = controller.GetPorts();

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var ports = okResult.Value.Should().BeAssignableTo<List<PortInfo>>().Subject;
        ports.Should().ContainSingle();
        ports[0].Name.Should().Be("COM3");
        ports[0].Description.Should().BeEmpty();
    }
}
