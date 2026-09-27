namespace APConfigManager.Core.Models
{
    /// <summary>
    /// A persisted, human-readable record of an operation the operator performed
    /// (connect, flash, parameters, bootloader, …). This is the user-facing action
    /// journal, distinct from <c>ILogger</c> diagnostics.
    /// </summary>
    public class OperationJournalEntry
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>When the operation finished (UTC).</summary>
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Short operation label, e.g. "Connect", "Firmware", "Parameters", "Bootloader".</summary>
        public string Operation { get; set; } = string.Empty;

        /// <summary>Serial port the operation ran on (e.g. "COM3", "/dev/ttyACM0").</summary>
        public string Port { get; set; } = string.Empty;

        /// <summary>Board serial, when known.</summary>
        public string DeviceSerial { get; set; } = string.Empty;

        /// <summary>Profile applied, when the operation used one.</summary>
        public string ProfileName { get; set; } = string.Empty;

        /// <summary>Whether the operation succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Human-readable outcome detail.</summary>
        public string Message { get; set; } = string.Empty;
    }
}
