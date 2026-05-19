using APConfigManager.Core.Models;
using APConfigManager.Core.Models.Settings;

namespace APConfigManager.Core.Data
{
    /// <summary>
    /// Allows you to save the configuration for each board type.
    /// </summary>
    public interface IDeviceProfileRepository
    {
        /// <summary>
        /// List of all saved profiles
        /// </summary>
        List<DeviceProfile> GetAll();

        /// <summary>
        /// Find a profile by its identifier.
        /// </summary>
        DeviceProfile? GetById(Guid profileId);

        /// <summary>
        /// Find a profile by board type
        /// </summary>
        DeviceProfile? GetByBoardType(uint boardType);

        /// <summary>
        /// Save/Update Profile
        /// </summary>
        void Save(DeviceProfile profile);

        /// <summary>
        ///  Delete Profile by Id
        /// </summary>
        void Delete(Guid profileId);
    }
}
