using Microsoft.AspNetCore.SignalR;

namespace APConfigManager.Api.Hubs
{
    /// <summary>
    /// SignalR hub for real-time device events.
    /// Clients subscribe to session-specific groups to receive progress updates and state changes.
    /// </summary>
    public class DeviceHub : Hub
    {
        /// <summary>
        /// Adds the calling client to a SignalR group for the specified session.
        /// </summary>
        public async Task SubscribeToSession(string sessionId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
        }

        /// <summary>
        /// Removes the calling client from a session group.
        /// </summary>
        public async Task UnsubscribeFromSession(string sessionId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, sessionId);
        }


    }
}
