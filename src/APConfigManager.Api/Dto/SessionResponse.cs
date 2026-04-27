namespace APConfigManager.Api.Dto
{
    /// <summary>
    /// Response representing an active device session.
    /// </summary>
    public class SessionResponse
    {
        public Guid Id { get; set; }

        public string Port { get; set; } = string.Empty;

        public int BaudRate { get; set; }

        public string State { get; set; } = string.Empty;

        public DateTime? ConnectedAt { get; set; } = DateTime.UtcNow;
    }
}
