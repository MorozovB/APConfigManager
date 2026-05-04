using APConfigManager.Api.Dto;
using APConfigManager.Api.Hubs;
using APConfigManager.Core.Enums;
using APConfigManager.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace APConfigManager.Api.Controllers
{
    /// <summary>
    /// Handles device boot mode switching.
    /// </summary>
    [ApiController]
    [Route("api/sessions/{sessionId:guid}/boot")]
    public class BootController : ControllerBase
    {
        private readonly ISessionManager sessionManager;

        private readonly IHubContext<DeviceHub> hubContext;


        public BootController(ISessionManager sessionManager, IHubContext<DeviceHub> hubContext)
        {
            this.sessionManager = sessionManager;
            this.hubContext = hubContext;
        }

        /// <summary>
        /// POST /api/sessions/{id}/boot — boots the device from bootloader to normal mode.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<OperationResultResponse>> Boot(
    Guid sessionId,
    CancellationToken ct)
        {
            var session = sessionManager.GetSession(sessionId);
            if (session is null)
            {
                return NotFound(new OperationResultResponse
                {
                    Success = false,
                    Message = "Session not found"
                });
            }

            try
            {
                var driver = sessionManager.GetDriver(sessionId);
                var result = await driver.RebootAsync(BootMode.Normal, ct);

                if (result.Success)
                {
                    await hubContext.Clients.Group(sessionId.ToString())
                        .SendAsync("DeviceStateChanged", sessionId.ToString(), "Connected", ct);
                }

                return Ok(new OperationResultResponse
                {
                    Success = result.Success,
                    Message = result.Success ? "Device booted successfully" : result.ErrorMessage,
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new OperationResultResponse
                {
                    Success = false,
                    Message = $"Failed to boot device: {ex.Message}"
                });
            }
        }
    }
}
