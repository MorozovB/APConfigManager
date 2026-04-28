using APConfigManager.Api.Dto;
using APConfigManager.Api.Hubs;
using APConfigManager.Core.Exceptions;
using APConfigManager.Core.Interfaces.Services;
using APConfigManager.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace APConfigManager.Api.Controllers
{
    /// <summary>
    /// Handles parameter upload, download, and reset operations.
    /// </summary>
    [ApiController]
    [Route("api/sessions/{sessionId:guid}/params")]
    public class ParamsController : ControllerBase
    {
        private readonly IParamService paramService;

        private readonly IHubContext<DeviceHub> hubContext;

        public ParamsController(IParamService paramService, IHubContext<DeviceHub> hubContext)
        {
            this.paramService = paramService;
            this.hubContext = hubContext;
        }


        /// <summary>
        /// POST /api/sessions/{id}/params/upload — uploads .param file to the device.
        /// </summary>
        [HttpPost("upload")]
        public async Task<ActionResult<OperationResultResponse>> Upload(Guid sessionId, IFormFile file, CancellationToken ct)
        {
            if (file is null || file.Length <= 0)
            {
                return BadRequest("Parameter file is required");
            }

            try
            {
                var stream = file.OpenReadStream();

                var progress = new  Progress<(int current, int total)>(p =>
                {
                    hubContext.Clients.Group(sessionId.ToString())
                        .SendAsync("ParamProgress", p.current, p.total);
                });

                var result = await paramService.UploadAsync(sessionId, stream, progress, ct);

                await hubContext.Clients.Group(sessionId.ToString())
                    .SendAsync("OperationCompleted", sessionId.ToString(), result);

                return Ok(new OperationResultResponse
                {
                    Success = result.Success,
                    Message = result.ErrorMessage ?? "Parameters uploaded",
                    Data = result
                });
            }
            catch (SessionException ex)
            {
                return NotFound(ex.Message);
            }
        }

        /// <summary>
        /// GET /api/sessions/{id}/params — reads all parameters from the device.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<Parameter>>> Download(Guid sessionId, CancellationToken ct)
        {
            try
            {
                var parameters = await paramService.DownloadAsync(sessionId, ct);

                return Ok(parameters);
            }
            catch(SessionException ex)
            {
                return  NotFound(ex.Message);
            }
        }

        /// <summary>
        /// POST /api/sessions/{id}/params/reset — resets parameters to factory defaults.
        /// </summary>
        [HttpPost("reset")]
        public async Task<ActionResult<OperationResultResponse>> Reset(Guid sessionId, CancellationToken ct)
        {
            try
            {
                await paramService.ResetAsync(sessionId, ct);

                return Ok(new OperationResultResponse
                {
                    Success = true,
                    Message = "Parameters reset to factory defaults",
                });
            }
            catch (SessionException ex)
            {
                return NotFound(ex.Message);
            }
        }
    }
}
