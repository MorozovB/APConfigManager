namespace APConfigManager.Core.Exceptions
{
    /// <summary>
    /// Session manager errors.
    /// </summary>
    public class SessionException : Exception
    {
        /// <summary>
        /// Creates an exception with a session error description.
        /// </summary>
        public SessionException(string message) : base(message) { }

        /// <summary>
        /// Creates an exception with a description and an inner exception.
        /// </summary>
        public SessionException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
