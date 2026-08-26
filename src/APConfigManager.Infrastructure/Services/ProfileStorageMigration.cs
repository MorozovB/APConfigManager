using APConfigManager.Core.Data;
using APConfigManager.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace APConfigManager.Infrastructure.Services
{
    public class ProfileStorageMigration
    {
        private readonly IDeviceProfileRepository repository;
        private readonly IProfileFileService files;
        private readonly ILogger<ProfileStorageMigration> logger;

        public ProfileStorageMigration(IDeviceProfileRepository repository,
            IProfileFileService files, ILogger<ProfileStorageMigration> logger)
        {
            this.repository = repository; this.files = files; this.logger = logger;
        }

        public void Run()
        {
            foreach (var profile in repository.GetAll())
            {
                var fw = files.EnsureInProfileFolder(profile.Id, profile.FirmwareFilePath);
                var pr = files.EnsureInProfileFolder(profile.Id, profile.ParameterFilePath);

                if (fw != profile.FirmwareFilePath || pr != profile.ParameterFilePath)
                {
                    profile.FirmwareFilePath = fw;
                    profile.ParameterFilePath = pr;
                    repository.Save(profile);
                    logger.LogInformation("Migrated profile {Id} files into its folder", profile.Id);
                }
            }
        }
    }
}

