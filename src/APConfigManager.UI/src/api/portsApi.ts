import apiClient from './apiClient';
import { PortInfo } from '../types/device';

export const getPorts = async (): Promise<PortInfo[]> => {
  const response = await apiClient.get<PortInfo[]>('/ports');
  return response.data;
};
