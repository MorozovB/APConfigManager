namespace APConfigManager.Api.Dto
{
    /// <summary>
    /// Request body for creating a new device session.
    /// </summary>
    public class CreateSessionRequest
    {
        public string Port { get; set; } = string.Empty;

        public int BaudRate { get; set; } = 115200;
    }
}
