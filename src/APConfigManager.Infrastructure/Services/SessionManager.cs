using APConfigManager.Core.Enums;
using APConfigManager.Core.Exceptions;
using APConfigManager.Core.Interfaces.Drivers;
using APConfigManager.Core.Interfaces.Services;
using APConfigManager.Core.Models;

namespace APConfigManager.Infrastructure.Services;

/// <summary>
/// Manages up to 4 concurrent device sessions. Tracks active connections
/// and prevents duplicate port usage.
/// </summary>
public class SessionManager : ISessionManager, IAsyncDisposable
{
    private const int MaxSessions = 4;

    private readonly Dictionary<Guid, DeviceSession> sessions = new();
    private readonly Dictionary<Guid, IAutopilotDriver> drivers = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Func<IAutopilotDriver> driverFactory;
    private readonly Dictionary<Guid, Action<float>> telemetryCallbacks = new();

    /// <summary>
    /// Initializes the session manager with a driver factory.
    /// The factory will be replaced with proper DI registration in Flasher.Api.
    /// </summary>  
    public SessionManager(Func<IAutopilotDriver> driverFactory)
    {
        this.driverFactory = driverFactory;
    }

    /// <summary>
    /// Creates a new session on the specified port.
    /// Throws SessionException if port is already in use or session limit reached.
    /// Connects to the device and adds the session to the active list.
    /// </summary>
    public async Task<DeviceSession> CreateSessionAsync(
        string port,
        int baudRate,
        CancellationToken ct)
    {
        await _lock.WaitAsync(ct);

        try
        {
            if (sessions.Count >= MaxSessions)
            {
                throw new SessionException(
                    $"Maximum of {MaxSessions} concurrent sessions reached.");
            }

            var portInUse = sessions.Values.Any(s =>
                s.Port.Equals(port, StringComparison.OrdinalIgnoreCase));

            if (portInUse)
            {
                throw new SessionException(
                    $"Port {port} is already in use by another session.");
            }

            var driver = this.driverFactory();
            var session = await driver.ConnectAsync(port, baudRate, ct);

            sessions[session.Id] = session;
            drivers[session.Id] = driver;

            return session;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Returns a session by its ID or null if not found.
    /// </summary>
    public DeviceSession? GetSession(Guid sessionId)
    {
        sessions.TryGetValue(sessionId, out var session);
        return session;
    }

    /// <summary>
    /// Returns all active sessions.
    /// </summary>
    public List<DeviceSession> GetAllSessions()
    {
        return sessions.Values.ToList();
    }

    /// <summary>
    /// Closes a session, disconnects the driver, and frees the port.
    /// Throws SessionException if session not found.
    /// </summary>
    public async Task CloseSessionAsync(Guid sessionId)
    {
        await _lock.WaitAsync();

        try
        {
            if (!drivers.TryGetValue(sessionId, out var driver))
            {
                throw new SessionException(
                    $"Session {sessionId} not found.");
            }

            await driver.DisconnectAsync();

            drivers.Remove(sessionId);
            sessions.Remove(sessionId);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Returns the autopilot driver for the specified session.
    /// Throws SessionException if session not found.
    /// </summary>
    public IAutopilotDriver GetDriver(Guid sessionId)
    {
        if (!drivers.TryGetValue(sessionId, out var driver))
        {
            throw new SessionException(
                $"Session {sessionId} not found.");
        }

        return driver;
    }

    public void SetTelemetryCallback(Guid sessionId, Action<float> onAltitude)
    {
        telemetryCallbacks[sessionId] = onAltitude;
        if (drivers.TryGetValue(sessionId, out var driver))
            driver.StartTelemetryAsync(onAltitude);
    }

    public void SyncSessionFromDriver(Guid sessionId)
    {
        if (!drivers.TryGetValue(sessionId, out var driver))
        {
            return;
        }

        var current = driver.GetCurrentSession();
        if (current is null)
        {
            return;
        }

        sessions[sessionId] = current;
    }

    /// <summary>
    /// Closes all active sessions and releases resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _lock.WaitAsync();

        try
        {
            foreach (var driver in drivers.Values)
            {
                try
                {
                    await driver.DisconnectAsync();
                }
                catch
                {
                    // Suppress errors during cleanup
                }
            }

            drivers.Clear();
            sessions.Clear();
        }
        finally
        {
            _lock.Release();
            _lock.Dispose();
        }
    }
}
