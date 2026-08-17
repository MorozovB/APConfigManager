using System.Collections.Concurrent;
using APConfigManager.Core.Interfaces.Services;
using Microsoft.AspNetCore.SignalR;

namespace APConfigManager.Api.Hubs
{
    /// <summary>
    /// SignalR hub for real-time device events. Clients subscribe to a per-session group.
    /// Contract: the client gets the session's INITIAL state from the HTTP response
    /// (POST /api/sessions … SessionResponse.State) and then relies on "DeviceStateChanged"
    /// events for subsequent updates. The hub does not push initial state on subscribe.
    /// </summary>
    public class DeviceHub : Hub
    {
        // connectionId -> sessions this connection watches.
        // SignalR serializes calls per connection, so each set is single-threaded per connection.
        private static readonly ConcurrentDictionary<string, HashSet<Guid>> Subscriptions = new();

        private readonly ISessionManager sessionManager;

        public DeviceHub(ISessionManager sessionManager)
        {
            this.sessionManager = sessionManager;
        }

        /// <summary>Adds the calling client to the session's group.</summary>
        public async Task SubscribeToSession(Guid sessionId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, sessionId.ToString());

            _ = Subscriptions.GetOrAdd(Context.ConnectionId, _ => new HashSet<Guid>()).Add(sessionId);
        }

        /// <summary>Removes the calling client from the session's group.</summary>
        public async Task UnsubscribeFromSession(Guid sessionId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, sessionId.ToString());
            if (Subscriptions.TryGetValue(Context.ConnectionId, out var set))
            {
                _ = set.Remove(sessionId);
            }
        }

        /// <summary>On client disconnect, stop telemetry for the sessions it was watching.</summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (Subscriptions.TryRemove(Context.ConnectionId, out var sessions))
            {
                foreach (var sessionId in sessions)
                {
                    await sessionManager.StopTelemetryAsync(sessionId);
                }
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}
