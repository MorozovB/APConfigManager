using APConfigManager.Core.Data;
using APConfigManager.Core.Models.Settings;
using APConfigManager.Infrastructure.Data;

public class SettingsRepository : ISettingsRepository
{
    public readonly LiteDbContext context;
    private static readonly object _gate = new();

    public SettingsRepository(LiteDbContext context)
    {
        this.context = context;
    }

    public AppSettings GetSettings()
    {
        lock (_gate)
        {
            var settings = context.Settings.Query().FirstOrDefault();
            if (settings is not null)
            {
                return settings;
            }

            settings = new AppSettings { Language = "UA" };
            _ = context.Settings.Insert(settings);
            return settings;
        }
    }

    public void SaveSettings(AppSettings settings)
    {
        lock (_gate)
        {
            var existing = context.Settings.Query().FirstOrDefault();
            if (existing is not null)
            {
                settings.Id = existing.Id;
            }

            _ = context.Settings.Upsert(settings);
        }
    }
}
