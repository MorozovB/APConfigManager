using APConfigManager.Core.Data;
using APConfigManager.Core.Models.Settings;

namespace APConfigManager.Infrastructure.Data
{
    /// <summary>
    /// Stores and retrieves application settings from LiteDB (single document).
    /// </summary>
    public class SettingsRepository : ISettingsRepository
    {
        public readonly LiteDbContext context;

        public SettingsRepository(LiteDbContext context)
        {
            this.context = context;
        }

        /// <summary>
        /// Returns current settings or creates default ones if none exist.
        /// </summary>
        public AppSettings GetSettings()
        {
            var settings = context.Settings.FindAll().FirstOrDefault();

            if ( settings is not null)
            {
                return settings;
            }

            settings = new AppSettings
            {
                Language = "ua"
            };

            context.Settings.Insert(settings);
            return settings;
        }

        /// <summary>
        /// Saves or updates the application settings.
        /// </summary>
        public void SaveSettings(AppSettings settings)
        {
            context.Settings.Update(settings);
        }
    }
}
