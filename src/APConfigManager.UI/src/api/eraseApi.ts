import apiClient from './apiClient';
import { OperationResult } from '../types/operations';

export const startErase = async (sessionId: string): Promise<OperationResult> => {
  const response = await apiClient.post<OperationResult>(
    `/sessions/${sessionId}/erase`,
    null,
    { timeout: 300000 }
  );
  return response.data;
};
