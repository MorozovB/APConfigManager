namespace APConfigManager.Core.Models
{
    /// <summary>
    /// COM port information including USB device details for device identification.
    /// </summary>
    public class PortDescription
    {
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string DeviceSerial { get; set; } = string.Empty;

        public string VendorId { get; set; } = string.Empty;

        public string ProductId { get; set; } = string.Empty;

        public bool IsMavlink { get; set; }
    }
}
