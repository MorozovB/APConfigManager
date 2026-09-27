namespace APConfigManager.Api.Dto
{
    /// <summary>
    /// Request body for appending an operation-journal entry. The server stamps
    /// the id and timestamp; the client supplies the operation context.
    /// </summary>
    public class CreateJournalEntryRequest
    {
        public string Operation { get; set; } = string.Empty;
        public string Port { get; set; } = string.Empty;
        public string DeviceSerial { get; set; } = string.Empty;
        public string ProfileName { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
