using APConfigManager.Api.Dto;
using APConfigManager.Core.Data;
using APConfigManager.Core.Interfaces.Services;
using APConfigManager.Core.Models.Settings;
using Microsoft.AspNetCore.Mvc;

namespace APConfigManager.Api.Controllers;

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
    public ActionResult<List<ProfileResponse>> GetAll()
    {
        var profiles = repository.GetAll();
        return Ok(profiles.Select(ProfileResponse.From).ToList());
    }

    [HttpPost]
    public ActionResult<ProfileResponse> Create([FromBody] SaveProfileRequest request)
    {
        if (request is null)
        {
            return BadRequest("Profile is required");
        }

        var profile = MapToDomain(Guid.NewGuid(), request);
        NormalizeProfilePaths(profile);
        repository.Save(profile);

        var response = ProfileResponse.From(profile);
        return Created($"/api/profiles/{profile.Id}", response);
    }

    [HttpPut("{id:guid}")]
    public ActionResult<ProfileResponse> Update(Guid id, [FromBody] SaveProfileRequest request)
    {
        if (request is null)
        {
            return BadRequest("Profile is required");
        }

        var profile = MapToDomain(id, request);
        NormalizeProfilePaths(profile);
        repository.Save(profile);

        return Ok(ProfileResponse.From(profile));
    }

    [HttpDelete("{id:guid}")]
    public ActionResult Delete(Guid id)
    {
        repository.Delete(id);
        profileFileService.DeleteProfileFiles(id);
        return NoContent();
    }

    private static DeviceProfile MapToDomain(Guid id, SaveProfileRequest request) => new()
    {
        Id = id,
        Name = request.Name,
        Description = request.Description,
        BoardType = request.BoardType,
        ParameterFilePath = request.ParameterFilePath,
        FirmwareFilePath = request.FirmwareFilePath,
        ProfileOptions = request.ProfileOptions ?? new Dictionary<string, bool>
        {
            { "bootloader", false },
            { "firmware", false },
            { "parameters", false }
        }
    };

    private void NormalizeProfilePaths(DeviceProfile profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.FirmwareFilePath))
        {
            profile.FirmwareFilePath = profileFileService.EnsureInProfileFolder(profile.Id, profile.FirmwareFilePath);
        }

        if (!string.IsNullOrWhiteSpace(profile.ParameterFilePath))
        {
            profile.ParameterFilePath = profileFileService.EnsureInProfileFolder(profile.Id, profile.ParameterFilePath);
        }
    }
}
