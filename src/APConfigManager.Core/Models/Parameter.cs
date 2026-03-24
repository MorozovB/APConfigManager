namespace APConfigManager.Core.Models
{
    /// <summary>
    /// Represents autopilots parameter.
    /// </summary>
    public class Parameter
    {
        public string Name { get; init; } = string.Empty;

        public float Value { get; init; }
    }
}
