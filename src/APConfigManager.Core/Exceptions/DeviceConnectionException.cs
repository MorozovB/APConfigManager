namespace APConfigManager.Core.Exceptions
{
    /// <summary>
    /// Device connection errors.
    /// </summary>
    public class DeviceConnectionException : Exception
    {
        /// <summary>
        /// Creates an exception with a connection error description.
        /// </summary>
        public DeviceConnectionException(string message) : base(message) { }

        /// <summary>
        /// Creates an exception with a description and an inner exception.
        /// </summary>
        public DeviceConnectionException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
