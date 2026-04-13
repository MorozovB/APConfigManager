using APConfigManager.Core.Models.Settings;

namespace APConfigManager.Core.Data
{
    public interface ISettingsRepository
    {
        /// <summary>
        /// Get current settings (or default settings if not).
        /// </summary>
        /// <returns></returns>
        AppSettings GetSettings();

        /// <summary>
        /// Saving Settings
        /// </summary>
        void SaveSettings(AppSettings settings);
    }
}
