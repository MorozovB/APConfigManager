using APConfigManager.Api.Dto;
using APConfigManager.Api.Hubs;
using APConfigManager.Core.Exceptions;
using APConfigManager.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace APConfigManager.Api.Controllers;

/// <summary>
/// Handles flash memory erase operations.
/// </summary>
[ApiController]
[Route("api/sessions/{sessionId:guid}/erase")]
public class EraseController : ControllerBase
{
    private readonly IEraseService eraseService;
    private readonly IHubContext<DeviceHub> hubContext;

    public EraseController(IEraseService eraseService, IHubContext<DeviceHub> hubContext)
    {
        this.eraseService = eraseService;
        this.hubContext = hubContext;
    }

    /// <summary>
    /// POST /api/sessions/{id}/erase — starts flash memory erase.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<OperationResultResponse>> Erase(
        Guid sessionId,
        CancellationToken ct)
    {
        try
        {
            var progress = new Progress<(int percent, string message)>(async p =>
            {
                await hubContext.Clients.Group(sessionId.ToString())
                    .SendAsync("EraseProgress", p.percent, p.message);
            });

            var result = await eraseService.EraseAsync(sessionId, progress, ct);

            if (result.Success)
            {
                await hubContext.Clients.Group(sessionId.ToString())
                    .SendAsync("DeviceStateChanged", sessionId.ToString(), "Connected", ct);
            }

            await hubContext.Clients.Group(sessionId.ToString())
                .SendAsync("OperationCompleted", sessionId.ToString(), result, ct);

            return Ok(new OperationResultResponse
            {
                Success = result.Success,
                Message = result.ErrorMessage ?? "Erase completed",
                Data = result
            });
        }
        catch (SessionException ex)
        {
            return NotFound(ex.Message);
        }
    }
}
