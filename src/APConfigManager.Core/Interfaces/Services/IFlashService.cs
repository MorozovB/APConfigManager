using APConfigManager.Core.Results;

namespace APConfigManager.Core.Interfaces.Services
{
    /// <summary>
    /// Firmware service. Orchestrates the full cycle: parsing > validation > reboot > erase > write > verify > boot.
    /// </summary>
    public interface IFlashService
    {
        Task<FlashResult> FlashAsync(Guid sessionId, Stream stream,
            IProgress<(int percent, string message)> progress, CancellationToken ct);
    }
}
