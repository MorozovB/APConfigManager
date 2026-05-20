import apiClient from './apiClient';
import { OperationResult } from '../types/operations';

export const startFlash = async (sessionId: string, file: File): Promise<OperationResult> => {
  const formData = new FormData();

  formData.append('file', file);

  const response = await apiClient.post<OperationResult>(
    `/sessions/${sessionId}/flash`,
    formData,
    {
      headers: { 'Content-Type': 'multipart/form-data' },
      
      timeout: 600000,
    }
  );

  return response.data;
};
