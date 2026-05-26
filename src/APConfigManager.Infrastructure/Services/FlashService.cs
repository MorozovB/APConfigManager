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

    /// <summary>
    /// Executes the full flash cycle for the specified session.
    /// Parses the firmware file, validates compatibility with the device,
    /// checks if the version is already installed, then delegates flashing to the driver.
    /// </summary>
    //public async Task<FlashResult> FlashAsync(
    //    Guid sessionId,
    //    Stream stream,
    //    IProgress<(int percent, string message)> progress,
    //    CancellationToken ct)
    //{
    //    ArgumentNullException.ThrowIfNull(stream);
    //    ArgumentNullException.ThrowIfNull(progress);

    //    _ = sessionManager.GetSession(sessionId)
    //        ?? throw new SessionException($"Session {sessionId} not found.");

    //    var driver = sessionManager.GetDriver(sessionId);

    //    progress.Report((0, "Parsing firmware..."));
    //    var firmware = firmwareParser.Parse(stream);

    //    progress.Report((5, "Checking current version..."));
    //    var currentVersion = await driver.GetFirmwareVersionAsync(ct);

    //    if (!string.IsNullOrWhiteSpace(currentVersion)
    //        && !string.IsNullOrWhiteSpace(firmware.GitIdentity)
    //        && currentVersion.Equals(firmware.GitIdentity, StringComparison.OrdinalIgnoreCase))
    //    {
    //        return new FlashResult
    //        {
    //            Success = true,
    //            WasSameVersion = true,
    //            FirmwareVersion = firmware.Version
    //        };
    //    }

    //    progress.Report((10, "Reading device info..."));
    //    var deviceInfo = await driver.GetDeviceInfoAsync(ct);

    //    progress.Report((15, "Validating firmware..."));
    //    var validation = firmwareValidator.Validate(firmware, deviceInfo);

    //    if (!validation.IsValid)
    //    {
    //        return new FlashResult
    //        {
    //            Success = false,
    //            ErrorMessage = validation.ErrorMessage
    //        };
    //    }

    //    progress.Report((20, "Flashing..."));
    //    var flashProgress = new Progress<(int percent, string message)>(p =>
    //    {
    //        var scaled = 20 + (int)(p.percent * 0.8);
    //        progress.Report((scaled, p.message));
    //    });

    //    return await driver.FlashAsync(firmware, flashProgress, ct);
    //}


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

        progress.Report((0, "Parsing firmware..."));
        var firmware = firmwareParser.Parse(firmwareFile);

        // Skip version check if device is in bootloader (no firmware to query)
        if (session.State != DeviceState.InBootloader)
        {
            progress.Report((5, "Checking current version..."));
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

        progress.Report((10, "Reading device info..."));
        var deviceInfo = await driver.GetDeviceInfoAsync(ct);

        progress.Report((15, "Validating firmware..."));
        var validation = firmwareValidator.Validate(firmware, deviceInfo);

        if (!validation.IsValid)
        {
            return new FlashResult
            {
                Success = false,
                ErrorMessage = validation.ErrorMessage
            };
        }

        progress.Report((20, "Flashing..."));
        var flashProgress = new Progress<(int percent, string message)>(p =>
        {
            var scaled = 20 + (int)(p.percent * 0.8);
            progress.Report((scaled, p.message));
        });

        var result = await driver.FlashAsync(firmware, flashProgress, ct);

        if (result.Success)
        {
            sessionManager.SyncSessionFromDriver(sessionId);
        }

        return result;
    }
}
