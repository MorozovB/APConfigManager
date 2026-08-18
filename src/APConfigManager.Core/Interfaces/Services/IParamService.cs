using APConfigManager.Core.Models;
using APConfigManager.Core.Results;

namespace APConfigManager.Core.Interfaces.Services
{
    /// <summary>
    /// Parameter service.
    /// </summary>
    public interface IParamService
    {
        /// <summary>
        /// Upload settings from a file to a device.
        /// </summary>
        Task<ParameterUploadResult> UploadAsync(Guid sessionId, Stream stream,
           IProgress<(int percent, int total)> progress, CancellationToken ct);

        /// <summary>
        /// Read parameters from a device.
        /// </summary>
        Task<List<Parameter>> DownloadAsync(Guid sessionId, CancellationToken ct);

        /// <summary>
        /// Resetting Settings to Defaults
        /// </summary>
        Task<bool> ResetAsync(Guid sessionId, CancellationToken ct);
    }
}
