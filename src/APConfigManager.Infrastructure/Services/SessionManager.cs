using System.Collections.Concurrent;
using APConfigManager.Core.Data;
using APConfigManager.Core.Exceptions;
using APConfigManager.Core.Interfaces.Drivers;
using APConfigManager.Core.Interfaces.Services;
using APConfigManager.Core.Models;
using Microsoft.Extensions.Logging;

namespace APConfigManager.Infrastructure.Services;

/// <summary>
/// Manages concurrent device sessions (limit configurable via settings, 1–7).
/// Tracks active connections and prevents duplicate port usage.
/// </summary>
public class SessionManager : ISessionManager, IAsyncDisposable
{
    private const int HardMaxSessions = 7;

    private sealed record SessionEntry(DeviceSession Session, IAutopilotDriver Driver);

    private readonly ConcurrentDictionary<Guid, SessionEntry> sessions = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Func<IAutopilotDriver> driverFactory;
    private readonly ILogger<SessionManager> logger;

    private bool _disposed = false;

    public SessionManager(
        ILogger<SessionManager> logger,
        Func<IAutopilotDriver> driverFactory)
    {
        this.logger = logger;
        this.driverFactory = driverFactory;
    }

    private static int GetMaxSessions() => HardMaxSessions;

    public async Task<DeviceSession> CreateSessionAsync(string port, int baudRate, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var maxSessions = GetMaxSessions();

            if (sessions.Count >= maxSessions)
            {
                logger.LogWarning("Session limit reached ({Max})", maxSessions);
                throw new SessionLimitReachedException($"Maximum of {maxSessions} concurrent sessions reached.");
            }

            var portInUse = sessions.Values.Any(e =>
                e.Session.Port.Equals(port, StringComparison.OrdinalIgnoreCase));

            if (portInUse)
            {
                logger.LogWarning("Port {Port} already in use", port);
                throw new PortInUseException($"Port {port} is already in use by another session.");
            }

            var driver = this.driverFactory();
            DeviceSession session;
            try
            {
                session = await driver.ConnectAsync(port, baudRate, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Connect failed on {Port}, releasing driver", port);
                try { await driver.DisconnectAsync(); } catch { }
                throw;
            }

            sessions[session.Id] = new SessionEntry(session, driver);

            logger.LogInformation("Session {Id} opened on {Port}", session.Id, port);
            return session;
        }
        finally
        {
            _ = _lock.Release();
        }
    }

    public DeviceSession? GetSession(Guid sessionId)
        => sessions.TryGetValue(sessionId, out var entry) ? entry.Session : null;

    public List<DeviceSession> GetAllSessions()
        => sessions.Values.Select(e => e.Session).ToList();

    public async Task CloseSessionAsync(Guid sessionId)
    {
        await _lock.WaitAsync();
        try
        {
            if (!sessions.TryGetValue(sessionId, out var entry))
            {
                logger.LogWarning("Session {Id} not found", sessionId);
                throw new SessionNotFoundException($"Session {sessionId} not found.");
            }

            try
            {
                await entry.Driver.DisconnectAsync();
            }
            finally
            {
                _ = sessions.TryRemove(sessionId, out _);
            }

            logger.LogInformation("Session {Id} closed", sessionId);
        }
        finally
        {
            _ = _lock.Release();
        }
    }

    public IAutopilotDriver GetDriver(Guid sessionId)
    {
        if (!sessions.TryGetValue(sessionId, out var entry))
        {
            logger.LogWarning("Session {Id} not found", sessionId);
            throw new SessionNotFoundException($"Session {sessionId} not found.");
        }

        return entry.Driver;
    }

    public void SetTelemetryCallback(Guid sessionId, Action<float> onAltitude)
    {
        if (sessions.TryGetValue(sessionId, out var entry))
        {
            entry.Driver.StartTelemetry(onAltitude);
        }
    }

    public void SyncSessionFromDriver(Guid sessionId)
    {
        if (!sessions.TryGetValue(sessionId, out var entry))
        {
            return;
        }

        var current = entry.Driver.GetCurrentSession();
        if (current is not null)
        {
            // обновляем только если запись не удалили/не подменили — без «воскрешения» закрытой сессии
            _ = sessions.TryUpdate(sessionId, entry with { Session = current }, entry);
        }
    }

    public List<string> GetOccupiedPorts(Guid? excludeSessionId = null)
        => sessions.Values
            .Where(e => excludeSessionId == null || e.Session.Id != excludeSessionId)
            .Select(e => e.Session.Port)
            .ToList();

    public async Task StopTelemetryAsync(Guid sessionId)
    {
        if (sessions.TryGetValue(sessionId, out var entry))
        {
            await entry.Driver.StopTelemetryAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await _lock.WaitAsync();
        try
        {
            foreach (var entry in sessions.Values)
            {
                try { await entry.Driver.DisconnectAsync(); }
                catch (Exception ex) { logger.LogDebug(ex, "Error disconnecting driver during dispose"); }
            }
            sessions.Clear();
        }
        finally
        {
            _ = _lock.Release();
            _lock.Dispose();
        }
    }
}
