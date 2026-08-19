using APConfigManager.Core.Models;

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

        public string DeviceSerial { get; set; } = string.Empty;

        public string FirmwareVersion { get; set; } = string.Empty;

        public uint BootloaderRevision { get; set; }

        public string FirmwareDescription { get; set; } = string.Empty;

        public static SessionResponse From(DeviceSession session) => new()
        {
            Id = session.Id,
            Port = session.Port,
            BaudRate = session.BaudRate,
            State = session.State.ToString(),
            ConnectedAt = session.ConnectedAt,
            DeviceSerial = session.DeviceSerial,
            FirmwareVersion = session.FirmwareVersion,
            FirmwareDescription = session.FirmwareDescription,
            BootloaderRevision = session.BootloaderRevision
        };
    }
}
