namespace APConfigManager.Core.Models.Settings
{
    /// <summary>
    /// Allows you to remember settings for a specific AP.
    /// </summary>
    public class DeviceProfile
    {
        /// <summary>
        /// Unique profile identifier.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Custom name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the descriptive text associated with the object.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the board type identifier for the device profile.
        /// </summary>
        public uint BoardType { get; set; }

        /// <summary>
        /// Path to the default parameter file.
        /// </summary>
        public string? ParameterFilePath { get; set; } = string.Empty;

        /// <summary>
        /// Path to the default firmware file
        /// </summary>
        public string? FirmwareFilePath {  get; set; } = string.Empty;


        //-------Gets the names of files for firmware and param.---------
        public string? ParameterFileName =>
                string.IsNullOrWhiteSpace(ParameterFilePath)
                    ? null
                    : Path.GetFileName(ParameterFilePath);

        public string? FirmwareFileName =>
            string.IsNullOrWhiteSpace(FirmwareFilePath)
                ? null
                : Path.GetFileName(FirmwareFilePath);
        //----------------------------------------------------------------

        /// <summary>
        /// Default operation options for current devices profile.
        /// </summary>
        public Dictionary<string, bool> ProfileOptions { get; set; } = new Dictionary<string, bool>
        {
            { "bootloader", false },
            { "firmware", false },
            { "parameters", false }
        };
    }
}
