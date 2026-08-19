using APConfigManager.Api.Dto;
using APConfigManager.Api.Hubs;
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

            var stream = file.OpenReadStream();

            var progress = new  Progress<(int current, int total)>(p =>
            {
                _ = hubContext.Clients.Group(sessionId.ToString())
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

        /// <summary>
        /// GET /api/sessions/{id}/params — reads all parameters from the device.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<Parameter>>> Download(Guid sessionId, CancellationToken ct)
        {
            var parameters = await paramService.DownloadAsync(sessionId, ct);

            var response = parameters
                .Select(p => new ParameterResponse { Name = p.Name, Value = p.Value, ParamType = p.ParamType })
                .ToList();

            return Ok(response);
        }

        /// <summary>
        /// POST /api/sessions/{id}/params/reset — resets parameters to factory defaults.
        /// </summary>
        [HttpPost("reset")]
        public async Task<ActionResult<OperationResultResponse>> Reset(Guid sessionId, CancellationToken ct)
        {
            var success = await paramService.ResetAsync(sessionId, ct);

            return Ok(new OperationResultResponse
            {
                Success = success,
                Message = success
                    ? "Parameters reset to factory defaults"
                    : "Device did not confirm parameter reset",
            });
        }
    }
}
