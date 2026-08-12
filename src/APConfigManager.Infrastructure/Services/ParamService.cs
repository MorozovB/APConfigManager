using APConfigManager.Core.Exceptions;
using APConfigManager.Core.Interfaces.Parsers;
using APConfigManager.Core.Interfaces.Services;
using APConfigManager.Core.Models;
using APConfigManager.Core.Results;
using APConfigManager.Infrastructure.Drivers.Ardupilot;
using Microsoft.Extensions.Logging;

namespace APConfigManager.Infrastructure.Services
{
    /// <summary>
    /// Manages parameter upload (multi-pass), download, and reset operations.
    /// </summary>
    public class ParamService : IParamService
    {
        private readonly ISessionManager sessionManager;
        private readonly IParamFileParser paramParser;
        private readonly ILogger<ParamService> logger;

        /// <summary>
        /// Initializes the parameter service.
        /// </summary>
        public ParamService(ISessionManager sessionManager, IParamFileParser paramParser, ILogger<ParamService> logger)
        {
            this.sessionManager = sessionManager;
            this.paramParser = paramParser;
            this.logger = logger;
        }

        /// <summary>
        /// Parses the parameter file, then delegates upload to the driver.
        /// The driver handles mode switching and multi-pass retry internally.
        /// </summary>
        public async Task<ParameterUploadResult> UploadAsync(
            Guid sessionId,
            Stream stream,
            IProgress<(int percent, int total)> progress,
            CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentNullException.ThrowIfNull(progress);

            _ = sessionManager.GetSession(sessionId)
                ?? throw new SessionException($"Session {sessionId} not found.");

            var driver = sessionManager.GetDriver(sessionId);
            var parameters = paramParser.Parse(stream);

            var result = await driver.WriteParamsAsync(parameters, progress, ct);

            if (result.Success)
            {
                sessionManager.SyncSessionFromDriver(sessionId);
            }

            return result;
        }

        /// <summary>
        /// Reads all current parameters from the device.
        /// The driver handles mode switching internally.
        /// </summary>
        public async Task<List<Parameter>> DownloadAsync(
            Guid sessionId,
            CancellationToken ct)
        {
            _ = sessionManager.GetSession(sessionId)
                ?? throw new SessionException($"Session {sessionId} not found.");

            var driver = sessionManager.GetDriver(sessionId);

            sessionManager.SyncSessionFromDriver(sessionId);

            return await driver.ReadParamsAsync(ct);
        }

        /// <summary>
        /// Resets all parameters to factory defaults via MAVLink command.
        /// The driver handles mode switching and reboot internally.
        /// </summary>
        public async Task ResetAsync(Guid sessionId, CancellationToken ct)
        {
            _ = sessionManager.GetSession(sessionId)
                ?? throw new SessionException($"Session {sessionId} not found.");

            var driver = sessionManager.GetDriver(sessionId);
            await driver.ResetParamsAsync(ct);

            sessionManager.SyncSessionFromDriver(sessionId);
        }
    }
}


