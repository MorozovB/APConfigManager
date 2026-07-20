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
    private readonly object _stateLock = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Func<IAutopilotDriver> driverFactory;

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
    public async Task<DeviceSession> CreateSessionAsync(string port, int baudRate, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            lock (_stateLock)
            {
                if (sessions.Count >= MaxSessions)
                {
                    throw new SessionException($"Maximum of {MaxSessions} concurrent sessions reached.");
                }

                var portInUse = sessions.Values.Any(s =>
                    s.Port.Equals(port, StringComparison.OrdinalIgnoreCase));

                if (portInUse)
                {
                    throw new SessionException($"Port {port} is already in use by another session.");
                }
            }

            var driver = this.driverFactory();
            var session = await driver.ConnectAsync(port, baudRate, ct);

            lock (_stateLock)
            {
                sessions[session.Id] = session;
                drivers[session.Id] = driver;
            }

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
        lock (_stateLock)
        {
            sessions.TryGetValue(sessionId, out var session);
            return session;
        }
    }

    /// <summary>
    /// Returns all active sessions.
    /// </summary>
    public List<DeviceSession> GetAllSessions()
    {
        lock (_stateLock)
        {
            return sessions.Values.ToList();
        }
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
            IAutopilotDriver? driver;
            lock (_stateLock)
            {
                if (!drivers.TryGetValue(sessionId, out driver))
                {
                    throw new SessionException($"Session {sessionId} not found.");
                }
            }

            await driver.DisconnectAsync();

            lock (_stateLock)
            {
                drivers.Remove(sessionId);
                sessions.Remove(sessionId);
            }
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
        lock (_stateLock)
        {
            if (!drivers.TryGetValue(sessionId, out var driver))
            {
                throw new SessionException($"Session {sessionId} not found.");
            }

            return driver;
        }
    }

    /// <summary>
    /// Sets a telemetry callback for the specified session.
    /// The callback will be invoked with altitude updates.
    /// </summary>
    public void SetTelemetryCallback(Guid sessionId, Action<float> onAltitude)
    {
        IAutopilotDriver? driver;
        lock (_stateLock)
        {
            drivers.TryGetValue(sessionId, out driver);
        }

        driver?.StartTelemetry(onAltitude);
    }

    /// <summary>
    /// Session manager can sync the session state from the driver if needed.
    /// </summary>
    public void SyncSessionFromDriver(Guid sessionId)
    {
        IAutopilotDriver? driver;
        lock (_stateLock)
        {
            if (!drivers.TryGetValue(sessionId, out driver))
            {
                return;
            }
        }

        var current = driver.GetCurrentSession();
        if (current is null)
        {
            return;
        }

        lock (_stateLock)
        {
            sessions[sessionId] = current;
        }
    }

    /// <summary>
    /// Gets a list of occupied ports, optionally excluding a specific session ID.
    /// </summary>
    public List<string> GetOccupiedPorts(Guid? excludeSessionId = null)
    {
        lock (_stateLock)
        {
            return sessions.Values
                .Where(s => excludeSessionId == null || s.Id != excludeSessionId)
                .Select(s => s.Port)
                .ToList();
        }
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
