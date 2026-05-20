import apiClient from './apiClient';
import { DeviceSession, CreateSessionRequest } from '../types/session';

export const createSession = async (request: CreateSessionRequest): Promise<DeviceSession> => {
  const response = await apiClient.post<DeviceSession>('/sessions', request);
  return response.data;
};

export const getSessions = async (): Promise<DeviceSession[]> => {
  const response = await apiClient.get<DeviceSession[]>('/sessions');
  return response.data;
};

export const getSession = async (sessionId: string): Promise<DeviceSession> => {
  const response = await apiClient.get<DeviceSession>(`/sessions/${sessionId}`);
  return response.data;
};

export const closeSession = async (sessionId: string): Promise<void> => {
  await apiClient.delete(`/sessions/${sessionId}`);
};
