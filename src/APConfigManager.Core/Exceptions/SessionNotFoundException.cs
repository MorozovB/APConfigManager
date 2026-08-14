namespace APConfigManager.Core.Exceptions
{
    /// <summary>Requested session does not exist. Maps to 404.</summary>
    public class SessionNotFoundException : SessionException
    {
        public SessionNotFoundException(string message) : base(message) { }
        public SessionNotFoundException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
