using APConfigManager.Core.Models;

namespace APConfigManager.Api.Dto
{
    /// <summary>
    /// Response representing a single operation-journal entry.
    /// </summary>
    public class JournalEntryResponse
    {
        public Guid Id { get; set; }
        public DateTime TimestampUtc { get; set; }
        public string Operation { get; set; } = string.Empty;
        public string Port { get; set; } = string.Empty;
        public string DeviceSerial { get; set; } = string.Empty;
        public string ProfileName { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;

        public static JournalEntryResponse From(OperationJournalEntry e) => new()
        {
            Id = e.Id,
            TimestampUtc = e.TimestampUtc,
            Operation = e.Operation,
            Port = e.Port,
            DeviceSerial = e.DeviceSerial,
            ProfileName = e.ProfileName,
            Success = e.Success,
            Message = e.Message,
        };
    }
}
