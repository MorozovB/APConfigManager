using APConfigManager.Api.Dto;
using APConfigManager.Api.Hubs;
using APConfigManager.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using APConfigManager.Core.Exceptions;

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

            try
            {
                var session = await sessionManager.CreateSessionAsync(request.Port, request.BaudRate, ct);

                var response = new SessionResponse
                {
                    Id = session.Id,
                    Port = session.Port,
                    BaudRate = session.BaudRate,
                    State = session.State.ToString(),
                    ConnectedAt = session.ConnectedAt,
                    DeviceSerial = session.DeviceSerial,
                    FirmwareVersion = session.FirmwareVersion,
                    FirmwareDescription = session.FirmwareDescription,
                    BootloaderRevision = session.BootloaderRevision
                };

                await hubContext.Clients.Group(session.Id.ToString())
                    .SendAsync("DeviceStateChanged", session.Id.ToString(), "Connected", ct);

                return Created($"/api/sessions/{response.Id}", response);

            }
            catch (SessionException ex)
            {
                return Conflict(ex.Message);
            }
            catch (DeviceConnectionException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// GET /api/sessions — returns all active sessions.
        /// </summary>
        [HttpGet]
        public ActionResult<List<SessionResponse>> GetAllSessions()
        {
            var sessions = sessionManager.GetAllSessions();
            var response = sessions.Select(session => new SessionResponse
            {
                Id = session.Id,
                Port = session.Port,
                BaudRate = session.BaudRate,
                State = session.State.ToString(),
                ConnectedAt = session.ConnectedAt,
                DeviceSerial = session.DeviceSerial,
                FirmwareVersion = session.FirmwareVersion,
                FirmwareDescription = session.FirmwareDescription,
                BootloaderRevision = session.BootloaderRevision
            }).ToList();

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

            var response = new SessionResponse
            {
                Id = session.Id,
                Port = session.Port,
                BaudRate = session.BaudRate,
                State = session.State.ToString(),
                ConnectedAt = session.ConnectedAt,
                DeviceSerial = session.DeviceSerial,
                FirmwareVersion = session.FirmwareVersion,
                FirmwareDescription = session.FirmwareDescription,
                BootloaderRevision = session.BootloaderRevision
            };

            return Ok(response);
        }

        /// <summary>
        /// DELETE /api/sessions/{id} — closes a session and disconnects the device.
        /// </summary>
        [HttpDelete("{id:guid}")]
        public async Task<ActionResult> CloseSession(Guid id)
        {
            try
            {
                await sessionManager.CloseSessionAsync(id);

                await hubContext.Clients.Group(id.ToString())
                    .SendAsync("DeviceStateChanged", id.ToString(), "Disconnected");

                return NoContent();
            }
            catch (SessionException ex)
            {
                return NotFound(ex.Message);
            }
        }

    }
}
