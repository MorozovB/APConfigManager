namespace APConfigManager.Core.Results
{
    /// <summary>
    /// The result of a flash memory erase operation.
    /// </summary>
    public class EraseResult
    {
        /// <summary>
        ///  The result of switching between modes.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Runtime error message.
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
