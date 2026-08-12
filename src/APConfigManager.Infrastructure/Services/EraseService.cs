using APConfigManager.Core.Exceptions;
using APConfigManager.Core.Interfaces.Services;
using APConfigManager.Core.Results;
using APConfigManager.Infrastructure.Drivers.Ardupilot;
using Microsoft.Extensions.Logging;

namespace APConfigManager.Infrastructure.Services
{
    /// <summary>
    /// Orchestrates the flash memory erase cycle: reboot to bootloader, erase.
    /// </summary>
    public class EraseService : IEraseService
    {
        private readonly ISessionManager sessionManager;
        private readonly ILogger<EraseService> logger;

        public EraseService(ISessionManager sessionManager, ILogger<EraseService> logger)
        {
            this.sessionManager = sessionManager;
            this.logger = logger;
        }

        /// <summary>
        /// Executes the erase cycle for the specified session.
        /// </summary>
        public async Task<EraseResult> EraseAsync(Guid sessionId, IProgress<(int, string)> progress, CancellationToken ct)
        {
            logger.LogInformation("Erase requested for session {Id}", sessionId);

            ArgumentNullException.ThrowIfNull(progress);

            _ = sessionManager.GetSession(sessionId)
                ?? throw new SessionException($"Session {sessionId} not found.");

            var driver = sessionManager.GetDriver(sessionId);

            var result = await driver.EraseAsync(progress, ct);

            if (!result.Success)
            {
                logger.LogWarning("Erase failed for session {Id}: {Error}", sessionId, result.ErrorMessage);
            }

            if (result.Success)
            {
                sessionManager.SyncSessionFromDriver(sessionId);
            }

            return result;
        }
    }
}
