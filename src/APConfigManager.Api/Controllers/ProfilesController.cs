using APConfigManager.Core.Data;
using APConfigManager.Core.Interfaces.Services;
using APConfigManager.Core.Models.Settings;
using Microsoft.AspNetCore.Mvc;

namespace APConfigManager.Api.Controllers;

/// <summary>
/// Manages device profiles.
/// </summary>
[ApiController]
[Route("api/profiles")]
public class ProfilesController : ControllerBase
{
    private readonly IDeviceProfileRepository repository;
    private readonly IProfileFileService profileFileService;

    public ProfilesController(
        IDeviceProfileRepository repository,
        IProfileFileService profileFileService)
    {
        this.repository = repository;
        this.profileFileService = profileFileService;
    }

    [HttpGet]
    public ActionResult<List<DeviceProfile>> GetAll()
    {
        var profiles = repository.GetAll();
        return Ok(profiles);
    }

    [HttpPost]
    public ActionResult Save([FromBody] DeviceProfile profile)
    {
        if (profile is null)
        {
            return BadRequest("Profile is required");
        }

        NormalizeProfilePaths(profile);

        repository.Save(profile);
        return Ok();
    }

    [HttpDelete("{id:guid}")]
    public ActionResult Delete(Guid id)
    {
        repository.Delete(id);
        return NoContent();
    }

    private void NormalizeProfilePaths(DeviceProfile profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.FirmwareFilePath))
        {
            profile.FirmwareFilePath = profileFileService.ResolveStoredPath(
                profile.Id,
                profile.FirmwareFilePath);
        }

        if (!string.IsNullOrWhiteSpace(profile.ParameterFilePath))
        {
            profile.ParameterFilePath = profileFileService.ResolveStoredPath(
                profile.Id,
                profile.ParameterFilePath);
        }
    }
}
