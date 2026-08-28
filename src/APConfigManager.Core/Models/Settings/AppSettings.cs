namespace APConfigManager.Core.Models.Settings
{
    /// <summary>
    /// Custom application settings saved between launches.
    /// </summary>
    public class AppSettings
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Language { get; set; } = "UA";
        public string Theme { get; set; } = "dark";
    }
}
