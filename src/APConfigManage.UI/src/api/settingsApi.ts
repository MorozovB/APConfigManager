import apiClient from './apiClient';
import { AppSettings } from '../types/settings';

export const getSettings = async (): Promise<AppSettings> => {
  const response = await apiClient.get<AppSettings>('/settings');
  return response.data;
};

export const updateSettings = async (settings: AppSettings): Promise<void> => {
  await apiClient.put('/settings', settings);
};
