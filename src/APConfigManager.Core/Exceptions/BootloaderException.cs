namespace APConfigManager.Core.Exceptions
{
    /// <summary>
    /// STM32 bootloader protocol errors.
    /// </summary>
    public class BootloaderException : Exception
    {
        /// <summary>
        /// Creates an exception with a bootloader error description.
        /// </summary>
        public BootloaderException(string message) : base(message) { }

        /// <summary>
        /// Creates an exception with a description and an inner exception.
        /// </summary>
        public BootloaderException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
