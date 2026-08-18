using APConfigManager.Core.Interfaces.Services;
using APConfigManager.Core.Models;
using APConfigManager.Core.Results;

namespace APConfigManager.Infrastructure.Services;

/// <summary>
/// Validates firmware compatibility with the connected device.
/// </summary>
public class FirmwareValidator : IFirmwareValidator
{
    /// <summary>
    /// Checks if the firmware is compatible with the device.
    /// Verifies board ID match, image is not empty, and image fits in flash memory.
    /// </summary>
    public ValidationResult Validate(FirmwarePackage firmware, DeviceInfo device)
    {
        ArgumentNullException.ThrowIfNull(firmware);
        ArgumentNullException.ThrowIfNull(device);

        if (firmware.BoardId != device.BoardId)
        {
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Board ID mismatch: firmware={firmware.BoardId}, device={device.BoardId}."
            };
        }

        if (firmware.ImageBytes is null || firmware.ImageBytes.Length == 0)
        {
            return new ValidationResult { IsValid = false, ErrorMessage = "Firmware image is empty." };
        }

        if (firmware.ImageBytes.Length != firmware.ImageSize)
        {
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Firmware image size mismatch: decompressed {firmware.ImageBytes.Length} bytes, manifest declares {firmware.ImageSize}."
            };
        }

        if (firmware.ImageBytes.Length > device.FlashSize)
        {
            return new ValidationResult { IsValid = false, ErrorMessage = $"Firmware size ({firmware.ImageBytes.Length} bytes) exceeds flash capacity ({device.FlashSize} bytes)." };
        }

        return new ValidationResult
        {
            IsValid = true
        };
    }
}
