using APConfigManager.Api.Dto;
using APConfigManager.Api.Services;
using APConfigManager.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace APConfigManager.Api.Controllers
{
    [ApiController]
    [Route("api/sessions/{sessionId:guid}/boot")]
    public class BootController : ControllerBase
    {
        private readonly IBootService bootService;
        private readonly IDeviceNotifier notifier;

        public BootController(IBootService bootService, IDeviceNotifier notifier)
        {
            this.bootService = bootService;
            this.notifier = notifier;
        }

        [HttpPost]
        public async Task<ActionResult<OperationResultResponse>> Boot(Guid sessionId, CancellationToken ct)
        {
            var result = await bootService.BootAsync(sessionId, ct);

            if (result.Success)
            {
                notifier.StartTelemetryForwarding(sessionId);
                notifier.StateChanged(sessionId);
            }

            return Ok(new OperationResultResponse
            {
                Success = result.Success,
                Message = result.Success ? "Device booted successfully" : result.ErrorMessage,
                Data = result
            });
        }

        [HttpPost("update-bootloader")]
        public async Task<ActionResult<OperationResultResponse>> UpdateBootloader(Guid sessionId, CancellationToken ct)
        {
            var result = await bootService.UpdateBootloaderAsync(sessionId, ct);

            if (result.Success)
            {
                notifier.StartTelemetryForwarding(sessionId);
                notifier.StateChanged(sessionId);
            }

            return Ok(new OperationResultResponse
            {
                Success = result.Success,
                Message = result.Success ? "Bootloader updated successfully" : result.ErrorMessage,
                Data = result
            });
        }
    }
}
