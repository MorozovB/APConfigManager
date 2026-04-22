using APConfigManager.Core.Interfaces.Drivers;
using APConfigManager.Core.Models;

namespace APConfigManager.Core.Interfaces.Services
{
    /// <summary>
    /// Manage active device connections
    /// </summary>
    public interface ISessionManager
    {
        /// <summary>
        /// Create a new session.
        /// </summary>
        Task<DeviceSession> CreateSessionAsync(string port, int baudRate, CancellationToken ct);

        /// <summary>
        /// Getting a session by Id
        /// </summary>
        DeviceSession? GetSession(Guid sessionId);

        /// <summary>
        /// List of all active sessions
        /// </summary>
        List<DeviceSession> GetAllSessions();

        /// <summary>
        /// Closure of the session and release of the port.
        /// </summary>
        Task CloseSessionAsync(Guid sessionId);

        /// <summary>
        /// Get the driver associated with the session.
        /// </summary>
        IAutopilotDriver GetDriver(Guid sessionId);
    }
}
