using APConfigManager.Api.Dto;
using APConfigManager.Api.Hubs;
using APConfigManager.Api.Services;
using APConfigManager.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace APConfigManager.Api.Controllers;

/// <summary>
/// Handles firmware flashing operations.
/// </summary>
[ApiController]
[Route("api/sessions/{sessionId:guid}/flash")]
public class FlashController : ControllerBase
{
    private readonly IFlashService flashService;
    private readonly IDeviceNotifier notifier;

    public FlashController(
      IFlashService flashService,
      IDeviceNotifier notifier)
    {
        this.flashService = flashService;
        this.notifier = notifier;
    }


    /// <summary>
    /// POST /api/sessions/{id}/flash — starts firmware flashing.
    /// Accepts .apj file via multipart/form-data.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<OperationResultResponse>> Flash(
        Guid sessionId,
        IFormFile file,
        CancellationToken ct)
    {
        if (file is null || file.Length <= 0)
        {
            return BadRequest("Firmware file is required.");
        }
        using var stream = file.OpenReadStream();

        var progress = new Progress<(int percent, string message)>(p =>
            notifier.FlashProgress(sessionId, p.percent, p.message));

        var result = await flashService.FlashAsync(sessionId, stream, progress, ct);

        if (result.Success)
        {
            notifier.StartTelemetryForwarding(sessionId);
            notifier.StateChanged(sessionId);
        }

        notifier.OperationCompleted(sessionId, result);

        return Ok(new OperationResultResponse
        {
            Success = result.Success,
            Message = result.ErrorMessage ?? "Flash completed",
            Data = result
        });
    }
}
