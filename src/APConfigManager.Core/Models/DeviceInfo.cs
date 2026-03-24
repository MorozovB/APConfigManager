namespace APConfigManager.Core.Models
{
    /// <summary>
    /// Basic hardware information reported by the autopilot device.
    /// </summary>
    public class DeviceInfo
    {
        /// <summary>
        /// Unique board identifier
        /// </summary>
        public uint BoardId { get; init; }

        /// <summary>
        /// Hardware revision of the board.
        /// </summary>
        public uint BoardRevision { get; init; }

        /// <summary>
        /// Flash memory size in bytes!.
        /// </summary>
        public uint FlashSize { get; init; }

        /// <summary>
        /// Bootloader revision number.
        /// </summary>
        public uint BootloaderRevision { get; init; }
    }
}
