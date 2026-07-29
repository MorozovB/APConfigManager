using APConfigManager.Core.Results;

namespace APConfigManager.Core.Interfaces.Services
{
    /// <summary>
    /// Firmware service. Orchestrates the full cycle: parsing > validation > reboot > erase > write > verify > boot.
    /// </summary>
    public interface IFlashService
    {
        /// <summary>
        /// Аsynchronously flashes the firmware to the device.
        /// Returns a <see cref="FlashResult"/> indicating the success or failure of the operation.
        /// </summary>
        Task<FlashResult> FlashAsync(Guid sessionId, Stream stream,
            IProgress<(int percent, string message)> progress, CancellationToken ct);
    }
}
