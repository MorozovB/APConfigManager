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

        /// <summary>
        /// Updates the stored session from the driver's current connection state.
        /// </summary>
        void SyncSessionFromDriver(Guid sessionId);

        /// <summary>
        /// Setting a callback for receiving telemetry updates (e.g., altitude) from the driver,
        /// which will update the session's telemetry data in real-time.
        /// </summary>
        void SetTelemetryCallback(Guid sessionId, Action<float> onAltitude);

        /// <summary>
        /// List of all occupied ports by active sessions, optionally excluding a specific session
        /// (useful for checking port availability when updating a session).
        /// </summary>
        List<string> GetOccupiedPorts(Guid? excludeSessionId = null);

        /// <summary>
        /// Stops the telemetry loop for the session (e.g. when the UI client disconnects).
        /// </summary>
        Task StopTelemetryAsync(Guid sessionId);
    }
}
