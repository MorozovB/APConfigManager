import apiClient from './apiClient';
import { OperationResult, Parameter } from '../types/operations';

export const uploadParams = async (sessionId: string, file: File): Promise<OperationResult> => {
    const formData = new FormData();
    formData.append('file', file);

    const response = await apiClient.post<OperationResult>(
        `/sessions/${sessionId}/params/upload`,
        formData,
        {
            headers: { 'Content-Type': 'multipart/form-data' },
            timeout: 600000,
        }
    );

    return response.data;
};

export const readParams = async (sessionId: string): Promise<Parameter[]> => {
    const response = await apiClient.get<Parameter[]>(
        `/sessions/${sessionId}/params`,
        { timeout: 300000 }
    );
    return response.data;
};

export const resetParams = async (sessionId: string): Promise<OperationResult> => {
    const response = await apiClient.post<OperationResult>(
        `/sessions/${sessionId}/params/reset`,
        null,
        { timeout: 300000 }
    );
    return response.data;
};

export const getParameter = async (sessionId: string, name: string): Promise<Parameter> => {
    const response = await apiClient.get<Parameter>(
        `/sessions/${sessionId}/params/${name}`,
        { timeout: 30000 },
    );
    return response.data;
};

export const setParameter = async (sessionId: string, name: string, value: number): Promise<OperationResult> => {
    const response = await apiClient.post<OperationResult>(
        `/sessions/${sessionId}/params/set`,
        { name, value },
        { timeout: 30000 },
    );
    return response.data;
};