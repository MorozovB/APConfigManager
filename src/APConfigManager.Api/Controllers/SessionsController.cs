using APConfigManager.Api.Dto;
using APConfigManager.Api.Hubs;
using APConfigManager.Core.Enums;
using APConfigManager.Core.Interfaces.Services;
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

        public SessionsController(ISessionManager sessionManager, IHubContext<DeviceHub> hubContext)
        {
            this.sessionManager = sessionManager;
            this.hubContext = hubContext;
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
            var session = await sessionManager.CreateSessionAsync(request.Port, request.BaudRate, ct);

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
