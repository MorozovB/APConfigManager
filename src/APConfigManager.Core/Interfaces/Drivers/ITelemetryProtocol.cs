using APConfigManager.Core.Models;

namespace APConfigManager.Core.Interfaces.Drivers
{
    /// <summary>
    /// Protocol for MAVLink communication with autopilot in normal mode.
    /// </summary>
    public interface ITelemetryProtocol
    {
        /// <summary>
        /// Sending MAVLink Heartbeat.
        /// </summary>
        Task SendHeartbeatAsync(CancellationToken ct);

        /// <summary>
        /// MAVLink CMD 246 - reboot in bootloader.
        /// </summary>
        Task RebootingToBootloaderAsync(CancellationToken ct);

        /// <summary>
        /// Request all parameters from the device.
        /// </summary>
        Task<List<Parameter>> RequestAllParamsAsync(CancellationToken ct);

        /// <summary>
        /// Setting a single parameter (PARAM_SET).
        /// </summary>
        Task<bool> SetParamsAsync(Parameter parameter, CancellationToken ct);

        /// <summary>
        /// Getting the git-hash of the current firmware.
        /// </summary>
        Task<string> GetFirwareVersionAsync(CancellationToken ct);

        /// <summary>
        /// MAVLink CMD 245-reset parameters to defaults.
        /// </summary>
        Task ResetParamsAsync(CancellationToken ct);
    }
}
