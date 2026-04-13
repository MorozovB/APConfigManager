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

        public List<DeviceProfile> GetAll()
        {
            return context.DeviceProfiles.FindAll().ToList();
        }

        public DeviceProfile? GetByBoardType(uint boardType)
        {
            return context.DeviceProfiles.FindOne(dp => dp.BoardType == boardType);
        }

        public void Save(DeviceProfile profile)
        {
            if (profile.Id == Guid.Empty)
            {
                profile.Id = Guid.NewGuid();
                context.DeviceProfiles.Upsert(profile);
            }
            else
            {
                context.DeviceProfiles.Update(profile);
            }
        }

        public void Delete(Guid profileId)
        {
            context.DeviceProfiles.Delete(profileId);
        }
    }
}
