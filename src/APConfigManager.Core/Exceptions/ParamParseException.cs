namespace APConfigManager.Core.Exceptions
{
    /// <summary>
    /// Parameter file (.param) parsing errors.
    /// </summary>
    public class ParamParseException : Exception
    {
        /// <summary>
        /// Creates an exception with a parameter parsing error description.
        /// </summary>
        public ParamParseException(string message) : base(message) { }

        /// <summary>
        /// Creates an exception with a description and an inner exception.
        /// </summary>
        public ParamParseException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
