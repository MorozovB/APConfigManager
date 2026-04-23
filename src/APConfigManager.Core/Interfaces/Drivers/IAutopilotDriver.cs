using APConfigManager.Core.Enums;
using APConfigManager.Core.Models;
using APConfigManager.Core.Results;

namespace APConfigManager.Core.Interfaces.Drivers
{
    /// <summary>
    /// Contract for interacting with any autopilot. Defines a complete set of operations.
    /// </summary>
    public interface IAutopilotDriver
    {
        /// <summary>
        /// Connecting to the device via the COM port.
        /// </summary>
        Task<DeviceSession> ConnectAsync(string port, int baudRate, CancellationToken ct);

        /// <summary>
        /// Getting device information (from bootloader).
        /// </summary>
        Task<DeviceInfo> GetDeviceInfoAsync(CancellationToken ct);

        /// <summary>
        /// Full firmware cycle.
        /// </summary>
        Task<FlashResult> FlashAsync(FirmwarePackage firmware,
           IProgress<(int percent, string message)> progress, CancellationToken ct);

        /// <summary>
        /// Erasing Flash memory.
        /// </summary>
        Task<EraseResult> EraseAsync(IProgress<(int percent, string message)> progress, CancellationToken ct);

        /// <summary>
        /// Reading all parameters from the device.
        /// </summary>
        Task<List<Parameter>> ReadParamsAsync(CancellationToken ct);

        /// <summary>
        /// Write parameters to the device.
        /// </summary>
        Task<ParameterUploadResult> WriteParamsAsync(List<Parameter> parameters,
            IProgress<(int current, int total)> progress, CancellationToken ct);

        /// <summary>
        /// Rebooting to the specified mode.
        /// </summary>
        Task<BootResult> RebootAsync(BootMode mode, CancellationToken ct);

        /// <summary>
        /// Retrieves the firmware git hash from the connected device.
        /// </summary>
        Task<string> GetFirmwareVersionAsync(CancellationToken ct);

        /// <summary>
        /// Resets all parameters to factory defaults.
        /// </summary>
        Task ResetParamsAsync(CancellationToken ct);

        /// <summary>
        /// Disconnecting from the device
        /// </summary>
        Task DisconnectAsync();
    }
}
