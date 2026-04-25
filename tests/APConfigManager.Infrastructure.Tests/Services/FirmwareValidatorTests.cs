using APConfigManager.Core.Models;
using APConfigManager.Infrastructure.Services;
using FluentAssertions;

namespace APConfigManager.Infrastructure.Tests.Services;

public class FirmwareValidatorTests
{
    private readonly FirmwareValidator _validator = new();

    private static FirmwarePackage CreateFirmware(
        uint boardId = 140,
        byte[]? imageBytes = null)
    {
        return new FirmwarePackage
        {
            BoardId = boardId,
            Magic = "0x32415054",
            Version = "4.5.1",
            GitIdentity = "abc123",
            ImageBytes = imageBytes ?? new byte[1024]
        };
    }

    private static DeviceInfo CreateDevice(
        uint boardId = 140,
        uint flashSize = 2_097_152)
    {
        return new DeviceInfo
        {
            BoardId = boardId,
            BoardRevision = 1,
            FlashSize = flashSize,
            BootloaderRevision = 5
        };
    }

    [Fact]
    public void Validate_CompatibleFirmware_ReturnsValid()
    {
        var firmware = CreateFirmware();
        var device = CreateDevice();

        var result = _validator.Validate(firmware, device);

        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Validate_ImageExactlyFitsFlash_ReturnsValid()
    {
        var firmware = CreateFirmware(imageBytes: new byte[2_097_152]);
        var device = CreateDevice(flashSize: 2_097_152);

        var result = _validator.Validate(firmware, device);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_BoardIdMismatch_ReturnsInvalid()
    {
        var firmware = CreateFirmware(boardId: 140);
        var device = CreateDevice(boardId: 50);

        var result = _validator.Validate(firmware, device);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("mismatch");
    }

    [Fact]
    public void Validate_EmptyImage_ReturnsInvalid()
    {
        var firmware = CreateFirmware(imageBytes: Array.Empty<byte>());
        var device = CreateDevice();

        var result = _validator.Validate(firmware, device);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("empty");
    }

    [Fact]
    public void Validate_NullImage_ReturnsInvalid()
    {
        var firmware = CreateFirmware(imageBytes: null!);
        var device = CreateDevice();

        var nullFirmware = new FirmwarePackage
        {
            BoardId = 140,
            Magic = "0x32415054",
            ImageBytes = null!
        };

        var result = _validator.Validate(nullFirmware, device);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ImageExceedsFlashSize_ReturnsInvalid()
    {
        var firmware = CreateFirmware(imageBytes: new byte[3_000_000]);
        var device = CreateDevice(flashSize: 2_097_152);

        var result = _validator.Validate(firmware, device);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("exceeds");
    }

    [Fact]
    public void Validate_NullFirmware_ThrowsArgumentNullException()
    {
        var device = CreateDevice();

        var act = () => _validator.Validate(null!, device);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Validate_NullDevice_ThrowsArgumentNullException()
    {
        var firmware = CreateFirmware();

        var act = () => _validator.Validate(firmware, null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
