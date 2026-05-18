namespace APConfigManager.Core.Results
{
    /// <summary>
    /// The result of the bootloader update process, including success status and any error messages.
    /// </summary>
    public class BootloaderUpdateResult
    {
        /// <summary>
        /// Operation completed.
        /// </summary>
        public bool Success { get; set; }
        /// <summary>
        /// Runtime error message.
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
