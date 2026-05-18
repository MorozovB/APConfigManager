import apiClient from './apiClient';
import { OperationResult } from '../types/operations';

export const bootDevice = async (sessionId: string): Promise<OperationResult> => {
  const response = await apiClient.post<OperationResult>(
    `/sessions/${sessionId}/boot`
  );
  return response.data;
};

export const updateBootloader = async (sessionId: string): Promise<OperationResult> => {
  const response = await apiClient.post<OperationResult>(
    `/sessions/${sessionId}/boot/update-bootloader`,
    null,
    { timeout: 60000 }
  );
  return response.data;
};
