namespace APConfigManager.Core.Results
{
    /// <summary>
    /// The result of the device's transition between modes (Normal, Bootloader).
    /// </summary>
    public class BootResult
    {
        /// <summary>
        /// Operation completed.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// New COM-port name.
        /// </summary>
        public string? NewPort { get; set; }

        /// <summary>
        /// Runtime error message.
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
