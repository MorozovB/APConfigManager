namespace APConfigManager.Core.Exceptions
{
    /// <summary>
    /// Firmware file (.apj) parsing errors.
    /// </summary>
    public class ApjParseException : Exception
    {
        /// <summary>
        /// Creates an exception with an APJ parsing error description.
        /// </summary>
        public ApjParseException(string message) : base(message) { }

        /// <summary>
        /// Creates an exception with a description and an inner exception.
        /// </summary>
        public ApjParseException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
