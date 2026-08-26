namespace APConfigManager.Core.Interfaces.Services;

/// <summary>
/// Reads and stores profile firmware/parameter files on the local file system.
/// </summary>
public interface IProfileFileService
{
    /// <summary>
    /// Opens the firmware file stream for a profile. Caller must dispose the stream.
    /// </summary>
    (Stream Stream, string FileName) OpenFirmware(Guid profileId);

    /// <summary>
    /// Opens the parameter file stream for a profile. Caller must dispose the stream.
    /// </summary>
    (Stream Stream, string FileName) OpenParameters(Guid profileId);

    /// <summary>
    /// Saves an uploaded firmware file and returns its full path on disk.
    /// </summary>
    Task<string> SaveFirmwareAsync(Guid profileId, Stream content, string fileName, CancellationToken ct = default);

    /// <summary>
    /// Saves an uploaded parameter file and returns its full path on disk.
    /// </summary>
    Task<string> SaveParametersAsync(Guid profileId, Stream content, string fileName, CancellationToken ct = default);

    /// <summary>
    /// Resolves a stored path to an existing absolute file path when possible.
    /// </summary>
    string ResolveStoredPath(Guid profileId, string storedPath);

    /// <summary>
    /// Resolves a path to a full absolute path when the file exists.
    /// </summary>
    string NormalizePath(Guid profileId, string path);

    /// <summary>
    /// Deletes all stored files for the profile (its upload folder).
    /// </summary>
    void DeleteProfileFiles(Guid profileId);

    /// <summary>
    /// Guarantees that the stored path is within the profile folder.
    /// If it is not, returns null. If it is, returns the full absolute path.
    /// </summary>
    string? EnsureInProfileFolder(Guid profileId, string? storedPath);
}
