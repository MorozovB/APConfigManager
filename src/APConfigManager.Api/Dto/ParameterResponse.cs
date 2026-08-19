namespace APConfigManager.Api.Dto
{
    /// <summary>
    /// Response representing a single autopilot parameter.
    /// </summary>
    public class ParameterResponse
    {
        public string Name { get; set; } = string.Empty;
        public float Value { get; set; }
        public byte ParamType { get; set; }
    }
}
