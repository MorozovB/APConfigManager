namespace APConfigManager.Api.Dto
{
    /// <summary>Client-supplied fields for creating/updating a profile. No Id — the server owns it.</summary>
    public class SaveProfileRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public uint BoardType { get; set; }
        public string? ParameterFilePath { get; set; }
        public string? FirmwareFilePath { get; set; }
        public Dictionary<string, bool>? ProfileOptions { get; set; }
    }
}
