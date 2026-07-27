using APConfigManager.Core.Data;
using APConfigManager.Core.Interfaces.Services;
using APConfigManager.Core.Models.Settings;

namespace APConfigManager.Infrastructure.Services
{
    /// <summary>
    /// Reads profile files from paths stored in LiteDB (local machine paths).
    /// </summary>
    public class ProfileFileService : IProfileFileService
    {
        private readonly IDeviceProfileRepository repository;
        private readonly string profileFilesRoot;

        public ProfileFileService(IDeviceProfileRepository repository)
        {
            this.repository = repository;

            profileFilesRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "APConfigManager",
                "profile-files");

            Directory.CreateDirectory(profileFilesRoot);
        }

        public (Stream Stream, string FileName) OpenFirmware(Guid profileId)
        {
            var profile = GetProfileOrThrow(profileId);
            return OpenFile(profileId, profile.FirmwareFilePath, "Firmware file");
        }

        public (Stream Stream, string FileName) OpenParameters(Guid profileId)
        {
            var profile = GetProfileOrThrow(profileId);
            return OpenFile(profileId, profile.ParameterFilePath, "Parameter file");
        }

        public async Task<string> SaveFirmwareAsync(
            Guid profileId,
            Stream content,
            string fileName,
            CancellationToken ct = default)
        {
            return await SaveUploadedFileAsync(profileId, content, fileName, ct);
        }

        public async Task<string> SaveParametersAsync(
            Guid profileId,
            Stream content,
            string fileName,
            CancellationToken ct = default)
        {
            return await SaveUploadedFileAsync(profileId, content, fileName, ct);
        }

        public string ResolveStoredPath(Guid profileId, string storedPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(storedPath);

            var resolved = TryResolveExistingPath(profileId, storedPath.Trim());
            return resolved ?? storedPath.Trim();
        }

        public string NormalizePath(Guid profileId, string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            var trimmed = path.Trim();
            var resolved = TryResolveExistingPath(profileId, trimmed);

            if (resolved is not null)
            {
                return resolved;
            }

            if (Path.IsPathRooted(trimmed))
            {
                return Path.GetFullPath(trimmed);
            }

            return trimmed;
        }

        public static IEnumerable<string> GetCandidatePaths(
            string profileFilesRoot,
            Guid profileId,
            string storedPath)
        {
            var trimmed = storedPath.Trim();
            var fileName = Path.GetFileName(trimmed);
            var candidates = new List<string>();

            if (Path.IsPathRooted(trimmed))
            {
                candidates.Add(Path.GetFullPath(trimmed));
            }
            else
            {
                candidates.Add(Path.GetFullPath(trimmed));

                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    candidates.Add(Path.Combine(profileFilesRoot, profileId.ToString(), fileName));
                }
            }

            return candidates.Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private DeviceProfile GetProfileOrThrow(Guid profileId)
        {
            return repository.GetById(profileId)
                ?? throw new FileNotFoundException($"Profile '{profileId}' was not found.");
        }

        private (Stream Stream, string FileName) OpenFile(
            Guid profileId,
            string? filePath,
            string label)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new FileNotFoundException($"{label} path is not set for this profile.");
            }

            var resolved = TryResolveExistingPath(profileId, filePath.Trim());

            if (resolved is null)
            {
                var tried = string.Join("; ", GetCandidatePaths(profileFilesRoot, profileId, filePath));
                throw new FileNotFoundException(
                    $"{label} not found for '{filePath}'. Checked: {tried}.");
            }

            var stream = new FileStream(
                resolved,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

            return (stream, Path.GetFileName(resolved));
        }

        private string? TryResolveExistingPath(Guid profileId, string storedPath)
        {
            foreach (var candidate in GetCandidatePaths(profileFilesRoot, profileId, storedPath))
            {
                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }

            return null;
        }

        private async Task<string> SaveUploadedFileAsync(
            Guid profileId,
            Stream content,
            string fileName,
            CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(content);

            var safeName = Path.GetFileName(fileName);

            if (string.IsNullOrWhiteSpace(safeName))
            {
                throw new ArgumentException("File name is required.", nameof(fileName));
            }

            var profileDir = Path.Combine(profileFilesRoot, profileId.ToString());
            Directory.CreateDirectory(profileDir);

            var fullPath = Path.Combine(profileDir, safeName);

            await using var fileStream = new FileStream(
                fullPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None);

            await content.CopyToAsync(fileStream, ct);

            return Path.GetFullPath(fullPath);
        }
    }

}

