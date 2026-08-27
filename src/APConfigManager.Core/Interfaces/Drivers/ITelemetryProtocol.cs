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
        Task RebootToBootloaderAsync(CancellationToken ct);

        /// <summary>
        /// Request all parameters from the device.
        /// </summary>
        Task<List<Parameter>> RequestAllParamsAsync(CancellationToken ct);

        /// <summary>
        /// Setting a single parameter (PARAM_SET).
        /// </summary>
        Task<bool> SetParamAsync(Parameter parameter, CancellationToken ct);

        /// <summary>
        /// Getting the git-hash of the current firmware.
        /// </summary>
        Task<string> GetFirmwareVersionAsync(CancellationToken ct);

        /// <summary>
        /// Waiting for a heartbeat message from the device, with a specified timeout.
        /// </summary>
        Task<bool> WaitForHeartbeatAsync(int timeoutMs, CancellationToken ct);

        /// <summary>
        /// MAVLink CMD 245-reset parameters to defaults.
        /// </summary>
        Task<bool> ResetParamsAsync(CancellationToken ct);

        /// <summary>
        /// MAVLink command to reboot the device into normal mode (if supported).
        /// </summary>
        Task RebootNormalAsync(CancellationToken ct);

        /// <summary>
        /// MAVLink command to update the bootloader firmware (if supported).
        /// </summary>
        Task<bool> FlashBootloaderAsync(CancellationToken ct);

        /// <summary>
        /// Reads boot messages from the device within a specified timeout.
        /// </summary>
        Task<List<string>> ReadBootMessagesAsync(int timeoutMs, CancellationToken ct);

        /// <summary>
        /// Reads telemetry data in a loop, invoking the provided callback with the altitude value whenever a new telemetry message is received.
        /// The loop continues until the cancellation token is triggered.
        /// </summary>
        Task ReadTelemetryLoopAsync(Action<float> onAltitude, Action? onDisconnected, CancellationToken ct);

        /// <summary>
        /// Gets the git hash of the firmware currently running on the device.
        /// </summary>
        Task<string> GetFirmwareGitHashAsync(CancellationToken ct);

        /// <summary>
        /// Checks if the core sensors of the device are healthy within a specified timeout.
        /// </summary>
        Task<bool> AreCoreSensorsHealthyAsync(int timeoutMs, CancellationToken ct);

        /// <summary>
        /// Reads a single parameter by name (PARAM_REQUEST_READ → PARAM_VALUE). Null if not returned.
        /// </summary>
        Task<Parameter?> ReadParameterAsync(string name, CancellationToken ct);
    }
}
