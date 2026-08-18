using APConfigManager.Core.Enums;
using APConfigManager.Core.Exceptions;
using APConfigManager.Core.Interfaces.Drivers;
using APConfigManager.Core.Interfaces.Parsers;
using APConfigManager.Core.Interfaces.Services;
using APConfigManager.Core.Models;
using APConfigManager.Core.Results;
using APConfigManager.Infrastructure.Drivers.Ardupilot;
using Microsoft.Extensions.Logging;

namespace APConfigManager.Infrastructure.Services;

/// <summary>
/// Orchestrates the full firmware flashing cycle: parse, validate,
/// flash, and verify. Delegates low-level operations to the driver.
/// </summary>
public class FlashService : IFlashService
{
    private readonly ISessionManager sessionManager;
    private readonly IFirmwareParser firmwareParser;
    private readonly IFirmwareValidator firmwareValidator;
    private readonly ILogger<FlashService> logger;

    /// <summary>
    /// Initializes the flash service with required dependencies.
    /// </summary>
    public FlashService(
        ISessionManager sessionManager,
        IFirmwareParser firmwareParser,
        IFirmwareValidator firmwareValidator,
        ILogger<FlashService> logger)
    {
        this.sessionManager = sessionManager;
        this.firmwareParser = firmwareParser;
        this.firmwareValidator = firmwareValidator;
        this.logger = logger;
    }

    public async Task<FlashResult> FlashAsync(
        Guid sessionId,
        Stream firmwareFile,
        IProgress<(int percent, string message)> progress,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(firmwareFile);
        ArgumentNullException.ThrowIfNull(progress);

        logger.LogInformation("Flash requested for session {Id}", sessionId);

        var session = sessionManager.GetSession(sessionId)
            ?? throw new SessionException($"Session {sessionId} not found.");

        var driver = sessionManager.GetDriver(sessionId);

        var firmware = firmwareParser.Parse(firmwareFile);

        await driver.StopTelemetryAsync();

        // Skip version check if device is in bootloader (no firmware to query)
        if (session.State != DeviceState.InBootloader)
        {
            try
            {
                var deviceHash = await driver.GetFirmwareGitHashAsync(ct);

                if (!string.IsNullOrWhiteSpace(deviceHash)
                    && !string.IsNullOrWhiteSpace(firmware.GitIdentity)
                    && firmware.GitIdentity.StartsWith(deviceHash, StringComparison.OrdinalIgnoreCase))
                {
                    var sameVersionResult = new FlashResult
                    {
                        Success = true,
                        WasSameVersion = true,
                        FirmwareVersion = firmware.Version
                    };

                    UpdateSessionFirmwareMetadata(driver, firmware, sameVersionResult);
                    sessionManager.SyncSessionFromDriver(sessionId);

                    return sameVersionResult;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Could not read firmware hash; proceeding to flash");
            }
        }

        var deviceInfo = await driver.GetDeviceInfoAsync(ct);

        var validation = firmwareValidator.Validate(firmware, deviceInfo);

        if (!validation.IsValid)
        {
            return new FlashResult
            {
                Success = false,
                ErrorMessage = validation.ErrorMessage
            };
        }

        var result = await driver.FlashAsync(firmware, progress, ct);

        if (result.Success)
        {
            UpdateSessionFirmwareMetadata(driver, firmware, result);
            sessionManager.SyncSessionFromDriver(sessionId);
        }

        if (!result.Success)
        {
            logger.LogWarning("Flash failed for session {Id}: {Error}", sessionId, result.ErrorMessage);
        }

        return result;
    }

    /// <summary>
    /// Metadata update after a successful flash.
    /// Updates the session's firmware version and description based on the flashed firmware package and the result from the driver.
    /// </summary>
    private static void UpdateSessionFirmwareMetadata(
        IAutopilotDriver driver,
        FirmwarePackage firmware,
        FlashResult result)
    {
        var currentSession = driver.GetCurrentSession();
        if (currentSession is null)
        {
            return;
        }

        var version = FirstNonEmpty(
            result.FirmwareVersion,
            firmware.Version,
            firmware.GitIdentity,
            currentSession.FirmwareVersion);

        if (!string.IsNullOrWhiteSpace(version))
        {
            currentSession.FirmwareVersion = version;
            if (string.IsNullOrWhiteSpace(result.FirmwareVersion))
            {
                result.FirmwareVersion = version;
            }
        }

        if (!string.IsNullOrWhiteSpace(firmware.Description))
        {
            currentSession.FirmwareDescription = firmware.Description;
        }
    }

    /// <summary>
    /// Returns the first non-empty string from the provided values, or an empty string if all are null or whitespace.
    /// </summary>
    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

}
