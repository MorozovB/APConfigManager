using APConfigManager.Core.Models.Settings;
using LiteDB;

namespace APConfigManager.Infrastructure.Data
{
    public class LiteDbContext : IDisposable
    {
        private readonly LiteDatabase _database;

        /// <summary>
        /// Initializes LiteDB at the specified file path.
        /// </summary>
        public LiteDbContext(string databasePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
            _database = new LiteDatabase(databasePath);
        }

        /// <summary>
        /// Settings collection (single document).
        /// </summary>
        public ILiteCollection<AppSettings> Settings =>
            _database.GetCollection<AppSettings>("settings");

        /// <summary>
        /// Device profiles collection.
        /// </summary>
        public ILiteCollection<DeviceProfile> DeviceProfiles =>
            _database.GetCollection<DeviceProfile>("device_profiles");

        /// <summary>
        /// Disposes the database connection.
        /// </summary>
        public void Dispose()
        {
            _database?.Dispose();
        }
    }
}
