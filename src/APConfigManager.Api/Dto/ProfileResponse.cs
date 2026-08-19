using APConfigManager.Core.Models.Settings;

namespace APConfigManager.Api.Dto
{
    /// <summary>Response representing a saved device profile.</summary>
    public class ProfileResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public uint BoardType { get; set; }
        public string? ParameterFilePath { get; set; }
        public string? FirmwareFilePath { get; set; }
        public string? ParameterFileName { get; set; }
        public string? FirmwareFileName { get; set; }
        public Dictionary<string, bool> ProfileOptions { get; set; } = new();

        public static ProfileResponse From(DeviceProfile p) => new()
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            BoardType = p.BoardType,
            ParameterFilePath = p.ParameterFilePath,
            FirmwareFilePath = p.FirmwareFilePath,
            ParameterFileName = p.ParameterFileName,
            FirmwareFileName = p.FirmwareFileName,
            ProfileOptions = p.ProfileOptions
        };
    }
}
