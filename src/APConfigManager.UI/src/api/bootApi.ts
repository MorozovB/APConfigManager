import apiClient from './apiClient';
import { OperationResult } from '../types/operations';
import { postOperation } from './operationClient';

export const bootDevice = async (sessionId: string): Promise<OperationResult> => {
    const response = await apiClient.post<OperationResult>(
        `/sessions/${sessionId}/boot`
    );
    return response.data;
};

export const updateBootloader = async (sessionId: string): Promise<OperationResult> =>
    postOperation(`/sessions/${sessionId}/boot/update-bootloader`);