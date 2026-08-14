namespace APConfigManager.Core.Exceptions
{
    /// <summary>Concurrent session limit reached. Maps to 429.</summary>
    public class SessionLimitReachedException : SessionException
    {
        public SessionLimitReachedException(string message) : base(message) { }
        public SessionLimitReachedException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
