namespace APConfigManager.Core.Results
{
    /// <summary>
    /// Represents flash operations result.
    /// </summary>
    public class FlashResult
    {
        /// <summary>
        ///  The firmware was completed successfully.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// True if the version matches.
        /// </summary>
        public bool WasSameVersion { get; set; }

        /// <summary>
        /// Number of bytes written.
        /// </summary>
        public long BytesWritten { get; set; }

        /// <summary>
        /// Firmware version that was written.
        /// </summary>
        public string? FirmwareVersion { get; set; }

        /// <summary>
        /// Runtime error message.
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
