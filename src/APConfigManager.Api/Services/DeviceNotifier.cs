using APConfigManager.Api.Hubs;
using APConfigManager.Core.Interfaces.Services;
using Microsoft.AspNetCore.SignalR;

namespace APConfigManager.Api.Services
{
    /// <summary>
    /// Single entry point for pushing device events to SignalR groups.
    /// </summary>
    public interface IDeviceNotifier
    {
        void FlashProgress(Guid sessionId, int percent, string message);
        void EraseProgress(Guid sessionId, int percent, string message);
        void ParamProgress(Guid sessionId, int current, int total);
        void StateChanged(Guid sessionId);
        void OperationCompleted(Guid sessionId, object result);
        void StartTelemetryForwarding(Guid sessionId);
    }

    /// <summary>
    /// Pushes device events to the per-session SignalR group. Every send is fire-and-forget
    /// but swallows delivery errors: a failed UI notification (e.g. client disconnected)
    /// must never break the ongoing device operation.
    /// </summary>
    public sealed class DeviceNotifier : IDeviceNotifier
    {
        private readonly IHubContext<DeviceHub> hub;
        private readonly ISessionManager sessionManager;
        private readonly ILogger<DeviceNotifier> logger;

        public DeviceNotifier(
            IHubContext<DeviceHub> hub,
            ISessionManager sessionManager,
            ILogger<DeviceNotifier> logger)
        {
            this.hub = hub;
            this.sessionManager = sessionManager;
            this.logger = logger;
        }

        public void FlashProgress(Guid sessionId, int percent, string message)
            => Send(sessionId, "FlashProgress", percent, message);

        public void EraseProgress(Guid sessionId, int percent, string message)
            => Send(sessionId, "EraseProgress", percent, message);

        public void ParamProgress(Guid sessionId, int current, int total)
            => Send(sessionId, "ParamProgress", current, total);

        public void StateChanged(Guid sessionId)
        {
            var state = sessionManager.GetSession(sessionId)?.State.ToString() ?? "Disconnected";
            Send(sessionId, "DeviceStateChanged", sessionId.ToString(), state);
        }

        public void OperationCompleted(Guid sessionId, object result)
            => Send(sessionId, "OperationCompleted", sessionId.ToString(), result);

        public void StartTelemetryForwarding(Guid sessionId)
        {
            sessionManager.SetTelemetryCallback(sessionId, altitude =>
                Send(sessionId, "AltitudeUpdate", altitude));
        }

        private void Send(Guid sessionId, string method, params object?[] args)
        {
            _ = SendSafeAsync(sessionId, method, args);
        }

        private async Task SendSafeAsync(Guid sessionId, string method, object?[] args)
        {
            try
            {
                await hub.Clients.Group(sessionId.ToString()).SendCoreAsync(method, args);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to deliver {Method} to session {Id}", method, sessionId);
            }
        }
    }
}
