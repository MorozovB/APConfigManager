using APConfigManager.Core.Models;
using APConfigManager.Infrastructure.Transport;
using FluentAssertions;

namespace APConfigManager.Infrastructure.Tests.Transport;

//public class PortScannerTests
//{
//    private readonly PortScanner scanner = new();

//    [Fact]
//    public void GetAvailablePorts_ReturnsListOfStrings()
//    {
//        var ports = scanner.GetAvailablePorts();
//        ports.Should().NotBeNull();
//        ports.Should().AllSatisfy(p => p.Should().StartWith("COM"));
//    }

//    [Fact]
//    public void GetAvailablePortsDetailed_ReturnsPortDescriptions()
//    {
//        var ports = scanner.GetAvailablePortsDetailed();
//        ports.Should().NotBeNull();
//        ports.Should().AllSatisfy(p =>
//        {
//            p.Name.Should().StartWith("COM");
//        });
//    }

//    [Fact]
//    public void GetPortDescription_NonExistingPort_ReturnsNull()
//    {
//        var result = scanner.GetPortDescription("COM999");
//        result.Should().BeNull();
//    }
//}
