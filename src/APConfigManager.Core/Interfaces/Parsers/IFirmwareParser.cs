using APConfigManager.Core.Models;

namespace APConfigManager.Core.Interfaces.Parsers
{
    /// <summary>
    /// Parsing the firmware file into a FirmwarePackage object.
    /// </summary>
    public interface IFirmwareParser
    {
        /// <summary>
        /// Parsing firmware from file.
        /// </summary>
        FirmwarePackage Parse(string filePath);

        /// <summary>
        /// Parsing firmware from stream.
        /// </summary>
        FirmwarePackage Parse(Stream stream);
    }
}
