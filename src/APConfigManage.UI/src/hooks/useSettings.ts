import { useState, useEffect, useCallback } from 'react';
import { AppSettings } from '../types/settings';
import { getSettings, updateSettings } from '../api/settingsApi';

export const useSettings = () => {
  const [settings, setSettings] = useState<AppSettings | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const data = await getSettings();
      setSettings(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load settings');
    } finally {
      setLoading(false);
    }
  }, []);

  const save = useCallback(async (newSettings: AppSettings) => {
    setError(null);
    try {
      await updateSettings(newSettings);
      setSettings(newSettings);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to save settings');
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  return { settings, loading, error, save, reload: load };
};
