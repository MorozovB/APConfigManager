using APConfigManager.Core.Enums;
using APConfigManager.Core.Exceptions;
using APConfigManager.Core.Interfaces.Parsers;
using APConfigManager.Core.Interfaces.Services;
using APConfigManager.Core.Results;

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

    /// <summary>
    /// Initializes the flash service with required dependencies.
    /// </summary>
    public FlashService(
        ISessionManager sessionManager,
        IFirmwareParser firmwareParser,
        IFirmwareValidator firmwareValidator)
    {
        this.sessionManager = sessionManager;
        this.firmwareParser = firmwareParser;
        this.firmwareValidator = firmwareValidator;
    }

    public async Task<FlashResult> FlashAsync(
        Guid sessionId,
        Stream firmwareFile,
        IProgress<(int percent, string message)> progress,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(firmwareFile);
        ArgumentNullException.ThrowIfNull(progress);

        var session = sessionManager.GetSession(sessionId)
            ?? throw new SessionException($"Session {sessionId} not found.");

        var driver = sessionManager.GetDriver(sessionId);

        var firmware = firmwareParser.Parse(firmwareFile);

        // Skip version check if device is in bootloader (no firmware to query)
        if (session.State != DeviceState.InBootloader)
        {
            try
            {
                var currentVersion = await driver.GetFirmwareVersionAsync(ct);

                if (!string.IsNullOrWhiteSpace(currentVersion)
                    && !string.IsNullOrWhiteSpace(firmware.GitIdentity)
                    && currentVersion.Equals(firmware.GitIdentity, StringComparison.OrdinalIgnoreCase))
                {
                    return new FlashResult
                    {
                        Success = true,
                        WasSameVersion = true,
                        FirmwareVersion = firmware.Version
                    };
                }
            }
            catch
            {
                // Device not responding — skip version check
            }
        }

        driver.StopTelemetry();

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
            sessionManager.SyncSessionFromDriver(sessionId);
        }

        return result;
    }
}
