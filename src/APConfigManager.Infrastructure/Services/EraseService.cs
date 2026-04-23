using APConfigManager.Core.Enums;
using APConfigManager.Core.Exceptions;
using APConfigManager.Core.Interfaces.Services;
using APConfigManager.Core.Results;

namespace APConfigManager.Infrastructure.Services
{
    /// <summary>
    /// Orchestrates the flash memory erase cycle: reboot to bootloader, erase, boot.
    /// </summary>
    public class EraseService : IEraseService
    {
        private readonly ISessionManager sessionManager;

        public EraseService(ISessionManager sessionManager)
        {
            this.sessionManager = sessionManager;
        }

        /// <summary>
        /// Executes the erase cycle for the specified session.
        /// </summary>
        public async Task<EraseResult> EraseAsync(Guid sessionId, IProgress<(int, string)> progress, CancellationToken ct)
        {
            var session = sessionManager.GetSession(sessionId);

            if (session is null)
            {
                throw new SessionException($"Session {sessionId} not found.");
            }

            var driver = sessionManager.GetDriver(sessionId);

            return await driver.EraseAsync(progress, ct);
        }
    }
}
