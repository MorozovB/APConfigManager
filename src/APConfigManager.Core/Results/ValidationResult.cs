namespace APConfigManager.Core.Results
{
    /// <summary>
    /// The result of firmware validation before flashing (checking compatibility with the device).
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// The firmware is supported by the device
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Runtime error message.
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
