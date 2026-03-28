using APConfigManager.Core.Models;
using APConfigManager.Core.Results;

namespace APConfigManager.Core.Interfaces.Services
{
    /// <summary>
    /// Firmware validation before recording. Checks for BoardId compatibility and data availability.
    /// </summary>
    public interface IFirmwareValidator
    {
        /// <summary>
        /// Checking the compatibility of the firmware with the device
        /// </summary>
        ValidationResult Validate(FirmwarePackage firmware, DeviceInfo device);
    }
}
