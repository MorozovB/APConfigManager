using APConfigManager.Core.Results;

namespace APConfigManager.Core.Interfaces.Services
{
    /// <summary>Orchestrates boot-mode operations for a session.</summary>
    public interface IBootService
    {
        Task<BootResult> BootAsync(Guid sessionId, CancellationToken ct);
        Task<BootloaderUpdateResult> UpdateBootloaderAsync(Guid sessionId, CancellationToken ct);
    }
}
