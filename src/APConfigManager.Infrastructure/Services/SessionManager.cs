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

    private readonly Dictionary<Guid, DeviceSession> _sessions = new();
    private readonly Dictionary<Guid, IAutopilotDriver> _drivers = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Func<IAutopilotDriver> _driverFactory;

    /// <summary>
    /// Initializes the session manager with a driver factory.
    /// The factory will be replaced with proper DI registration in Flasher.Api.
    /// </summary>
    public SessionManager(Func<IAutopilotDriver> driverFactory)
    {
        _driverFactory = driverFactory;
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
            if (_sessions.Count >= MaxSessions)
            {
                throw new SessionException(
                    $"Maximum of {MaxSessions} concurrent sessions reached.");
            }

            var portInUse = _sessions.Values.Any(s =>
                s.Port.Equals(port, StringComparison.OrdinalIgnoreCase));

            if (portInUse)
            {
                throw new SessionException(
                    $"Port {port} is already in use by another session.");
            }

            var driver = _driverFactory();
            var session = await driver.ConnectAsync(port, baudRate, ct);

            _sessions[session.Id] = session;
            _drivers[session.Id] = driver;

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
        _sessions.TryGetValue(sessionId, out var session);
        return session;
    }

    /// <summary>
    /// Returns all active sessions.
    /// </summary>
    public List<DeviceSession> GetAllSessions()
    {
        return _sessions.Values.ToList();
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
            if (!_drivers.TryGetValue(sessionId, out var driver))
            {
                throw new SessionException(
                    $"Session {sessionId} not found.");
            }

            await driver.DisconnectAsync();

            _drivers.Remove(sessionId);
            _sessions.Remove(sessionId);
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
        if (!_drivers.TryGetValue(sessionId, out var driver))
        {
            throw new SessionException(
                $"Session {sessionId} not found.");
        }

        return driver;
    }

    /// <summary>
    /// Closes all active sessions and releases resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _lock.WaitAsync();

        try
        {
            foreach (var driver in _drivers.Values)
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

            _drivers.Clear();
            _sessions.Clear();
        }
        finally
        {
            _lock.Release();
            _lock.Dispose();
        }
    }
}
