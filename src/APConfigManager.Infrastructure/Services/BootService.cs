using APConfigManager.Core.Enums;
using APConfigManager.Core.Exceptions;
using APConfigManager.Core.Interfaces.Services;
using APConfigManager.Core.Results;

namespace APConfigManager.Infrastructure.Services
{
    public sealed class BootService : IBootService
    {
        private readonly ISessionManager sessionManager;

        public BootService(ISessionManager sessionManager)
        {
            this.sessionManager = sessionManager;
        }

        public async Task<BootResult> BootAsync(Guid sessionId, CancellationToken ct)
        {
            _ = sessionManager.GetSession(sessionId)
                ?? throw new SessionNotFoundException($"Session {sessionId} not found.");

            var driver = sessionManager.GetDriver(sessionId);
            var result = await driver.RebootAsync(BootMode.Normal, ct);

            if (result.Success)
            {
                sessionManager.SyncSessionFromDriver(sessionId);
            }

            return result;
        }

        public async Task<BootloaderUpdateResult> UpdateBootloaderAsync(Guid sessionId, CancellationToken ct)
        {
            _ = sessionManager.GetSession(sessionId)
                ?? throw new SessionNotFoundException($"Session {sessionId} not found.");

            var driver = sessionManager.GetDriver(sessionId);
            var result = await driver.UpdateBootloaderAsync(ct);

            if (result.Success)
            {
                sessionManager.SyncSessionFromDriver(sessionId);
            }

            return result;
        }
    }
}
