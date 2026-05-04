using APConfigManager.Api.Controllers;
using APConfigManager.Core.Data;
using APConfigManager.Core.Models.Settings;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace APConfigManager.Api.Tests.Controllers;

public class SettingsControllerTests
{
    private readonly Mock<ISettingsRepository> mockSettingsRepository;
    private readonly SettingsController controller;

    public SettingsControllerTests()
    {
        mockSettingsRepository = new Mock<ISettingsRepository>();

        mockSettingsRepository
            .Setup(r => r.GetSettings())
            .Returns(new AppSettings { Language = "UA" });

        controller = new SettingsController(mockSettingsRepository.Object);
    }

    [Fact]
    public void GetSettings_ReturnsOkWithSettings()
    {
        var result = controller.GetSettings();

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var settings = okResult.Value.Should().BeOfType<AppSettings>().Subject;
        settings.Language.Should().Be("UA");
    }

    [Fact]
    public void UpdateSettings_ValidSettings_ReturnsNoContent()
    {
        var settings = new AppSettings { Language = "EN" };

        var result = controller.UpdateSettings(settings);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public void UpdateSettings_ValidSettings_CallsRepository()
    {
        var settings = new AppSettings { Language = "EN" };

        controller.UpdateSettings(settings);

        mockSettingsRepository.Verify(
            r => r.SaveSettings(It.Is<AppSettings>(s => s.Language == "EN")),
            Times.Once);
    }

    [Fact]
    public void UpdateSettings_NullSettings_ReturnsBadRequest()
    {
        var result = controller.UpdateSettings(null!);

        result.Should().BeOfType<BadRequestResult>();
    }
}
