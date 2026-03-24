using APConfigManager.Core.Models;

namespace APConfigManager.Core.Interfaces.Drivers
{
    /// <summary>
    /// Protocol for communicating with the bootloader.
    /// Low-level commands: sync, read information, erase, write in chunks, and verify the CRC.
    /// </summary>
    public interface IBootloaderProtocol
    {
        /// <summary>
        /// Syncing with the loader (GET_SYNC)
        /// </summary>
        Task<bool> SyncAsync(CancellationToken ct);

        /// <summary>
        /// Getting device information.
        /// </summary>
        Task<DeviceInfo> GetDeviceInfoAsync(CancellationToken ct);

        /// <summary>
        /// Complete chip erasure.
        /// </summary>
        Task ChipEraseAsync(CancellationToken ct);

        /// <summary>
        /// Recording a single data chunk.
        /// </summary>
        Task ProgramMultiAsync(byte[] data, CancellationToken ct);

        /// <summary>
        /// CRC verification of recorded data.
        /// </summary>
        Task<bool> VerifyCrcAsync(uint expectedCrc, CancellationToken ct);

        /// <summary>
        /// Changing the port speed on the bootloader side.
        /// </summary>
        Task SetBaudRateAsync(int Baudrate, CancellationToken ct);

        /// <summary>
        /// Command to the bootloader to run the firmware (exit bootloader).
        /// </summary>
        Task BootAsync(CancellationToken ct);

    }
}
