namespace APConfigManager.Core.Results
{
    /// <summary>
    /// Result of downloading parameters to the AP.
    /// </summary>
    public class ParameterUploadResult
    {
        /// <summary>
        /// Parameters upload successfully.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Number of successfully sent parameters
        /// </summary>
        public int Sent { get; set; }

        /// <summary>
        /// Total number of parameters in the file
        /// </summary>
        public int Total { get; set; }

        /// <summary>
        /// Number of parameters that could not be loaded
        /// </summary>
        public int Failed { get; set; }

        /// <summary>
        /// Number of skipped read-only parameters
        /// </summary>
        public int ReadOnly { get; set; }

        /// <summary>
        /// Number of hidden/non-existent parameters
        /// </summary>
        public int Hidden { get; set; }

        /// <summary>
        /// Runtime error message.
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
