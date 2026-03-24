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
        public Guid Id { get; init; }

        /// <summary>
        /// COM-port name.
        /// </summary>
        public string Port { get; init; } = string.Empty;

        /// <summary>
        /// Connection speed.
        /// </summary>
        public int BaudRate { get; init; }

        /// <summary>
        /// Current device status.
        /// </summary>
        public DeviceState State { get; init; }

        /// <summary>
        /// Session creation time
        /// </summary>
        public DateTime? ConnectedAt { get; set; } = DateTime.UtcNow;
    }
}
