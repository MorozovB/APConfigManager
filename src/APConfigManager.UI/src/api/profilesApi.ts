import apiClient from './apiClient';
import { DeviceProfile } from '../types/profile';

export const getProfiles = async (): Promise<DeviceProfile[]> => {
  const response = await apiClient.get<DeviceProfile[]>('/profiles');
  return response.data;
};

export const saveProfile = async (profile: DeviceProfile): Promise<void> => {
  await apiClient.post('/profiles', profile);
};

export const deleteProfile = async (profileId: string): Promise<void> => {
  await apiClient.delete(`/profiles/${profileId}`);
};
