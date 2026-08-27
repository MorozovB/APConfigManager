namespace APConfigManager.Api.Dto;

/// <summary>
/// DTO for setting a single parameter on the device. Contains the parameter name and the value to set.
/// </summary>
public class SetParameterRequest
{
    public string Name { get; set; } = string.Empty;
    public float Value { get; set; }
}
