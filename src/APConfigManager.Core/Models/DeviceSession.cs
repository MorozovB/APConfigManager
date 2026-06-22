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

        /// <summary>
        /// Display version of the firmware (if available).
        /// </summary>
        public string FirmwareVersion { get; set; } = string.Empty;


        /// <summary>
        /// Display version of the bootloader (if available).
        /// </summary>
        public uint BootloaderRevision { get; set; }
        
        /// <summary>
        /// Human-readable firmware description.
        /// </summary>
        public string FirmwareDescription { get; set; } = string.Empty;

        /// <summary>
        /// Display altitude of the device after the last telemetry update.
        /// </summary>
        public float LastAltitude { get; set; }

        /// <summary>
        /// Timestamp of the last telemetry update.
        /// </summary>
        public DateTime LastTelemetryAt { get; set; }

        /// <summary>
        /// Remember the USB location of the device for session management and reconnection purposes.
        /// </summary>
        public string UsbLocation { get; set; } = string.Empty;
    }
}
