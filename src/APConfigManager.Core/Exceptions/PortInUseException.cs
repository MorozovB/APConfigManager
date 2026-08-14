namespace APConfigManager.Core.Exceptions
{
    /// <summary>Port is already held by another session. Maps to 409.</summary>
    public class PortInUseException : SessionException
    {
        public PortInUseException(string message) : base(message) { }
        public PortInUseException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
