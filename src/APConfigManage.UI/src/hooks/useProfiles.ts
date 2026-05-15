import { useState, useEffect, useCallback } from 'react';
import { DeviceProfile } from '../types/profile';
import { getProfiles, saveProfile, deleteProfile } from '../api/profilesApi';

export const useProfiles = () => {
  const [profiles, setProfiles] = useState<DeviceProfile[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getProfiles();
      setProfiles(data);
    } catch (err) {
      setProfiles([]);
      console.warn('Profiles API not available yet');
    } finally {
      setLoading(false);
    }
  }, []);

  const save = useCallback(async (profile: DeviceProfile) => {
    setError(null);
    try {
      await saveProfile(profile);
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to save profile');
    }
  }, [load]);

  const remove = useCallback(async (profileId: string) => {
    setError(null);
    try {
      await deleteProfile(profileId);
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to delete profile');
    }
  }, [load]);

  useEffect(() => {
    load();
  }, [load]);

  return { profiles, loading, error, save, remove, reload: load };
};
