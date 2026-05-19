import axios, { AxiosError } from 'axios';
import apiClient from './apiClient';

export interface ProfileFilePathResponse {
  path: string;
}

const fileNameFromPath = (path: string): string => {
  const parts = path.split(/[/\\]/);
  return parts[parts.length - 1] || path;
};

const parseApiError = async (error: unknown): Promise<string> => {
  if (!axios.isAxiosError(error)) {
    return error instanceof Error ? error.message : 'Unknown error';
  }

  const axiosError = error as AxiosError<Blob | { message?: string }>;
  const data = axiosError.response?.data;

  if (data instanceof Blob) {
    try {
      const text = await data.text();
      const json = JSON.parse(text) as { message?: string };
      if (json.message) {
        return json.message;
      }
      return text;
    } catch {
      return axiosError.message;
    }
  }

  if (data && typeof data === 'object' && 'message' in data && data.message) {
    return data.message;
  }

  return axiosError.message;
};

const fetchProfileFile = async (profileId: string, kind: 'firmware' | 'parameters'): Promise<Blob> => {
  try {
    const response = await apiClient.get<Blob>(`/profiles/${profileId}/${kind}`, {
      responseType: 'blob',
    });
    return response.data;
  } catch (error) {
    throw new Error(await parseApiError(error));
  }
};

export const fetchProfileFirmware = (profileId: string): Promise<Blob> =>
  fetchProfileFile(profileId, 'firmware');

export const fetchProfileParameters = (profileId: string): Promise<Blob> =>
  fetchProfileFile(profileId, 'parameters');

export const uploadProfileFirmware = async (
  profileId: string,
  file: File,
): Promise<string> => {
  const formData = new FormData();
  formData.append('file', file);

  const response = await apiClient.post<ProfileFilePathResponse>(
    `/profiles/${profileId}/firmware`,
    formData,
    { headers: { 'Content-Type': 'multipart/form-data' } },
  );

  return response.data.path;
};

export const uploadProfileParameters = async (
  profileId: string,
  file: File,
): Promise<string> => {
  const formData = new FormData();
  formData.append('file', file);

  const response = await apiClient.post<ProfileFilePathResponse>(
    `/profiles/${profileId}/parameters`,
    formData,
    { headers: { 'Content-Type': 'multipart/form-data' } },
  );

  return response.data.path;
};

export const blobToFile = (blob: Blob, path: string): File =>
  new File([blob], fileNameFromPath(path), { type: 'application/octet-stream' });

export const isAbsolutePath = (path: string): boolean =>
  /^[a-zA-Z]:\\/.test(path) || path.startsWith('\\\\') || path.startsWith('/');

export { fileNameFromPath };
