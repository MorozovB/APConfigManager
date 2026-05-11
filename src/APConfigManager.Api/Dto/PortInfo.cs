namespace APConfigManager.Api.Dto
{
    /// <summary>
    /// Information about an available COM port.
    /// </summary>
    public class PortInfo
    {
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;
    }
}
