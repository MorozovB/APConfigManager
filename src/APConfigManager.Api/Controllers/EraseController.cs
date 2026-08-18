using APConfigManager.Api.Dto;
using APConfigManager.Api.Services;
using APConfigManager.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace APConfigManager.Api.Controllers;

/// <summary>
/// Handles flash memory erase operations.
/// </summary>
[ApiController]
[Route("api/sessions/{sessionId:guid}/erase")]
public class EraseController : ControllerBase
{
    private readonly IEraseService eraseService;
    private readonly IDeviceNotifier notifier;

    public EraseController(IEraseService eraseService, IDeviceNotifier notifier)
    {
        this.eraseService = eraseService;
        this.notifier = notifier;
    }

    /// <summary>
    /// POST /api/sessions/{id}/erase — starts flash memory erase.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<OperationResultResponse>> Erase(
        Guid sessionId,
        CancellationToken ct)
    {

        var progress = new Progress<(int percent, string message)>(p =>
            notifier.EraseProgress(sessionId, p.percent, p.message));

        var result = await eraseService.EraseAsync(sessionId, progress, ct);

        if (result.Success)
        {
            notifier.StateChanged(sessionId);
        }

        notifier.OperationCompleted(sessionId, result);

        return Ok(new OperationResultResponse
        {
            Success = result.Success,
            Message = result.ErrorMessage ?? "Erase completed",
            Data = result
        });
    }
}
