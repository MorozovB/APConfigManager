using APConfigManager.Api.Dto;
using APConfigManager.Api.Hubs;
using APConfigManager.Core.Data;
using APConfigManager.Core.Enums;
using APConfigManager.Core.Interfaces.Services;
using APConfigManager.Core.Models.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
namespace APConfigManager.Api.Controllers
{
    /// <summary>
    /// Manages device sessions: create, list, get, close.
    /// </summary>
    [ApiController]
    [Route("api/sessions")]
    public class SessionsController : ControllerBase
    {
        private readonly ISessionManager sessionManager;
        private readonly IHubContext<DeviceHub> hubContext;
        private readonly ISettingsRepository settingsRepository;

        public SessionsController(
            ISessionManager sessionManager,
            IHubContext<DeviceHub> hubContext,
            ISettingsRepository settingsRepository)
        {
            this.sessionManager = sessionManager;
            this.hubContext = hubContext;
            this.settingsRepository = settingsRepository;
        }

        /// <summary>
        /// POST /api/sessions — creates a new device session.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<SessionResponse>> CreateSession([FromBody] CreateSessionRequest request,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Port))
            {
                return BadRequest("Port is required.");
            }

            // Global connection speed comes from settings, coerced to a safe value;
            // the per-request BaudRate is ignored so every port uses the same speed.
            var baudRate = PortBaudRates.Coerce(settingsRepository.GetSettings().PortBaudRate);
            var session = await sessionManager.CreateSessionAsync(request.Port, baudRate, ct);

            var response = SessionResponse.From(session);

            if (session.State != DeviceState.InBootloader)
            {
                sessionManager.SetTelemetryCallback(session.Id, altitude =>
                {
                    _ = hubContext.Clients.Group(session.Id.ToString())
                        .SendAsync("AltitudeUpdate", altitude);
                });
            }

            var state = sessionManager.GetSession(session.Id)?.State.ToString() ?? "Disconnected";
            await hubContext.Clients.Group(session.Id.ToString())
                .SendAsync("DeviceStateChanged", session.Id.ToString(), state, ct);

            return Created($"/api/sessions/{response.Id}", response);
        }

        /// <summary>
        /// GET /api/sessions — returns all active sessions.
        /// </summary>
        [HttpGet]
        public ActionResult<List<SessionResponse>> GetAllSessions()
        {
            var sessions = sessionManager.GetAllSessions();
            var response = sessions.Select(SessionResponse.From).ToList();

            return Ok(response);
        }

        /// <summary>
        /// GET /api/sessions/{id} — returns a specific session.
        /// </summary>
        [HttpGet("{id:guid}")]
        public ActionResult<SessionResponse> GetSession(Guid id)
        {
            var session = sessionManager.GetSession(id);
            if (session == null)
            {
                return NotFound();
            }

            var response = SessionResponse.From(session);

            return Ok(response);
        }

        /// <summary>
        /// DELETE /api/sessions/{id} — closes a session and disconnects the device.
        /// </summary>
        [HttpDelete("{id:guid}")]
        public async Task<ActionResult> CloseSession(Guid id)
        {

            await sessionManager.CloseSessionAsync(id);

            await hubContext.Clients.Group(id.ToString())
                .SendAsync("DeviceStateChanged", id.ToString(), "Disconnected");

            return NoContent();
        }

    }
}
