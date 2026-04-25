using APConfigManager.Core.Data;
using APConfigManager.Core.Models.Settings;

namespace APConfigManager.Infrastructure.Data
{
    /// <summary>
    /// Stores and retrieves device profiles from LiteDB.
    /// </summary>
    public class DeviceProfileRepository : IDeviceProfileRepository
    {
        public readonly LiteDbContext context;

        public DeviceProfileRepository(LiteDbContext context)
        {
            this.context = context;
        }

        /// <summary>
        /// Returns all saved device profiles.
        /// </summary>
        public List<DeviceProfile> GetAll()
        {
            return context.DeviceProfiles.FindAll().ToList();
        }

        /// <summary>
        /// Finds a profile matching the given board type.
        /// </summary>
        public DeviceProfile? GetByBoardType(uint boardType)
        {
            return context.DeviceProfiles.FindOne(dp => dp.BoardType == boardType);
        }

        /// <summary>
        /// Saves or updates a device profile.
        /// </summary>
        public void Save(DeviceProfile profile)
        {
            if (profile.Id == Guid.Empty)
            {
                profile.Id = Guid.NewGuid();
            }

            context.DeviceProfiles.Upsert(profile);
        }

        /// <summary>
        /// Deletes a device profile by its Id.
        /// </summary>
        public void Delete(Guid profileId)
        {
            context.DeviceProfiles.Delete(new LiteDB.BsonValue(profileId));
        }
    }
}
