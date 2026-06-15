using APConfigManager.Core.Enums;

namespace APConfigManager.Core.Models
{
    /// <summary>
    /// Represents one active connection to the device.
    /// </summary>
    public class DeviceSession
    {
        /// <summary>
        /// Unique session identifier.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// COM-port name.
        /// </summary>
        public string Port { get; set; } = string.Empty;

        /// <summary>
        /// Connection speed.
        /// </summary>
        public int BaudRate { get; set; }

        /// <summary>
        /// Current device status.
        /// </summary>
        public DeviceState State { get; set; }

        /// <summary>
        /// Session creation time
        /// </summary>
        public DateTime? ConnectedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Device serial number for session identification and management.
        /// </summary>
        public string DeviceSerial { get; set; } = string.Empty;

        //For test
        public string FirmwareVersion { get; set; } = string.Empty;

        public uint BootloaderRevision { get; set; }

        public string FirmwareDescription { get; set; } = string.Empty;

        public float LastAltitude { get; set; }

        public DateTime LastTelemetryAt { get; set; }

        public string UsbLocation { get; set; } = string.Empty;
    }
}
