using APConfigManager.Core.Results;

namespace APConfigManager.Core.Interfaces.Services
{
    /// <summary>
    /// Device flash memory erasing service.
    /// </summary>
    public interface IEraseService
    {
        /// <summary>
        /// Run erasing service.
        /// </summary>
        Task<EraseResult> EraseAsync(Guid sessionId,
            IProgress<(int percent, string message)> progress, CancellationToken ct);
    }
}
