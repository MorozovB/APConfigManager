using APConfigManager.Api.Dto;
using APConfigManager.Api.Hubs;
using APConfigManager.Core.Exceptions;
using APConfigManager.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace APConfigManager.Api.Controllers
{
    /// <summary>
    /// Handles firmware flashing operations.
    /// </summary>
    [ApiController]
    [Route("api/sessions/{sessionId:guid}/flash")]
    public class FlashController : ControllerBase
    {
        private readonly IEraseService eraseService;

        private readonly IHubContext<DeviceHub> hubContext;

        public FlashController(IEraseService eraseService, IHubContext<DeviceHub> hubContext)
        {
            this.eraseService = eraseService;
            this.hubContext = hubContext;
        }

        // <summary>
        // POST /api/sessions/{id}/erase — starts flash memory erase.
        // </summary>
        [HttpPost]
        public async Task<ActionResult<OperationResultResponse>> Erase(Guid sessionId, CancellationToken ct)
        {
            try
            {
                var progress = new Progress<(int percent, string message)>(p =>
                {
                    hubContext.Clients.Group(sessionId.ToString())
                        .SendAsync("EraseProgress", p.percent, p.message);
                });

                var result = await eraseService.EraseAsync(sessionId, progress, ct);

                await hubContext.Clients.Group(sessionId.ToString())
                    .SendAsync("OperationCompleted", sessionId.ToString(), result);

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
}
