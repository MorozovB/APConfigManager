using APConfigManager.Api.Dto;
using APConfigManager.Api.Hubs;
using APConfigManager.Core.Exceptions;
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
    private readonly ISessionManager sessionManager;
    private readonly IHubContext<DeviceHub> hubContext;

    public FlashController(
      IFlashService flashService,
      ISessionManager sessionManager,
      IHubContext<DeviceHub> hubContext)
    {
        this.flashService = flashService;
        this.sessionManager = sessionManager;
        this.hubContext = hubContext;
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

        try
        {
            using var stream = file.OpenReadStream();

            var progress = new Progress<(int percent, string message)>(p =>
            {
                _ = hubContext.Clients.Group(sessionId.ToString())
                    .SendAsync("FlashProgress", p.percent, p.message);
            });

            var result = await flashService.FlashAsync(sessionId, stream, progress, ct);

            if (result.Success)
            {
                StartTelemetryForwarding(sessionId);

                await hubContext.Clients.Group(sessionId.ToString())
                    .SendAsync("DeviceStateChanged", sessionId.ToString(), "Connected", ct);
            }

            await hubContext.Clients.Group(sessionId.ToString())
                .SendAsync("OperationCompleted", sessionId.ToString(), result, ct);

            return Ok(new OperationResultResponse
            {
                Success = result.Success,
                Message = result.ErrorMessage ?? "Flash completed",
                Data = result
            });
        }
        catch (SessionException ex)
        {
            return NotFound(ex.Message);
        }
        catch (BootloaderException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (DeviceConnectionException ex)
        {
            return StatusCode(503, ex.Message);
        }
    }

    private void StartTelemetryForwarding(Guid sessionId)
    {
        sessionManager.SetTelemetryCallback(sessionId, altitude =>
        {
            _ = hubContext.Clients.Group(sessionId.ToString())
                .SendAsync("AltitudeUpdate", altitude);
        });
    }

}
