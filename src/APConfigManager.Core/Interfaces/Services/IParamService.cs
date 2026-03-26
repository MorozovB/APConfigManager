using APConfigManager.Core.Models;
using APConfigManager.Core.Results;

namespace APConfigManager.Core.Interfaces.Services
{
    /// <summary>
    /// Parameter service.
    /// </summary>
    internal interface IParamService
    {
        /// <summary>
        /// Upload settings from a file to a device.
        /// </summary>
        Task<ParameterUploadResult> UploadAsync(Guid sessionId, Stream stream,
           IProgress<(int percent, string message)> progress, CancellationToken ct);

        /// <summary>
        /// Read parameters from a device.
        /// </summary>
        Task<List<Parameter>> DawnloadAsync(Guid sessionId, CancellationToken ct);

        /// <summary>
        /// Resetting Settings to Defaults
        /// </summary>
        Task ResetAsync(Guid sessionId, CancellationToken ct);
    }
}
