namespace APConfigManager.Core.Models
{
    /// <summary>
    /// Represents a parsed firmware package used for updating the autopilot.
    /// Contains metadata and binary images extracted from the firmware file.
    /// </summary>
    public class FirmwarePackage
    {
        /// <summary>
        /// Magic number identifying the firmware file format.
        /// </summary>
        public uint Magic { get; init; }

        /// <summary>
        /// Target board identifier for which the firmware is intended.
        /// </summary>
        public uint BoardId { get; init; }

        // <summary>
        /// Firmware version (semantic or vendor-specific).
        /// </summary>
        public string Version { get; init; } = string.Empty;

        /// <summary>
        /// Git commit or build identity associated with this firmware.
        /// </summary>
        public string GitIdentity { get; init; } = string.Empty;

        /// <summary>
        /// Human-readable firmware description.
        /// </summary>
        public string Description { get; init; } = string.Empty;

        /// <summary>
        /// Main firmware image
        /// </summary>
        public byte[] ImageBytes { get; init; } = [];

        /// <summary>
        /// Image for external flash memory(optional)
        /// </summary>
        public byte[]? ExtfImageBytes { get; init; }
    }
}
