using APConfigManager.Api.Dto;
using APConfigManager.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace APConfigManager.Api.Controllers;

/// <summary>
/// Serves firmware and parameter files for device profiles from paths stored in LiteDB.
/// </summary>
[ApiController]
[Route("api/profiles/{profileId:guid}")]
public class ProfileFilesController : ControllerBase
{
    private readonly IProfileFileService profileFileService;

    public ProfileFilesController(IProfileFileService profileFileService)
    {
        this.profileFileService = profileFileService;
    }

    /// <summary>
    /// GET /api/profiles/{id}/firmware — returns the firmware file bytes.
    /// </summary>
    [HttpGet("firmware")]
    public IActionResult GetFirmware(Guid profileId)
    {

        var (stream, fileName) = profileFileService.OpenFirmware(profileId);
        return File(stream, "application/octet-stream", fileName);
    }

    /// <summary>
    /// GET /api/profiles/{id}/parameters — returns the parameter file bytes.
    /// </summary>
    [HttpGet("parameters")]
    public IActionResult GetParameters(Guid profileId)
    {
        var (stream, fileName) = profileFileService.OpenParameters(profileId);
        return File(stream, "application/octet-stream", fileName);
    }

    /// <summary>
    /// POST /api/profiles/{id}/firmware — stores an uploaded firmware file and returns its full path.
    /// </summary>
    [HttpPost("firmware")]
    public async Task<ActionResult<ProfileFilePathResponse>> UploadFirmware(
        Guid profileId,
        IFormFile file,
        CancellationToken ct)
    {
        if (file is null || file.Length <= 0)
        {
            return BadRequest("Firmware file is required.");
        }

        await using var stream = file.OpenReadStream();
        var path = await profileFileService.SaveFirmwareAsync(profileId, stream, file.FileName, ct);

        return Ok(new ProfileFilePathResponse { Path = path });
    }

    /// <summary>
    /// POST /api/profiles/{id}/parameters — stores an uploaded parameter file and returns its full path.
    /// </summary>
    [HttpPost("parameters")]
    public async Task<ActionResult<ProfileFilePathResponse>> UploadParameters(
        Guid profileId,
        IFormFile file,
        CancellationToken ct)
    {
        if (file is null || file.Length <= 0)
        {
            return BadRequest("Parameter file is required.");
        }

        await using var stream = file.OpenReadStream();
        var path = await profileFileService.SaveParametersAsync(profileId, stream, file.FileName, ct);

        return Ok(new ProfileFilePathResponse { Path = path });
    }
}
