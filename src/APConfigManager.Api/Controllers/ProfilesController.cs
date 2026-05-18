using APConfigManager.Api.Dto;
using APConfigManager.Core.Data;
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

    public ProfilesController(IDeviceProfileRepository repository)
    {
        this.repository = repository;
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
            return BadRequest("Profile is required");

        repository.Save(profile);
        return Ok();
    }

    [HttpDelete("{id:guid}")]
    public ActionResult Delete(Guid id)
    {
        repository.Delete(id);
        return NoContent();
    }
}
