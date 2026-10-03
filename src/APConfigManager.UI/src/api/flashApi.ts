import { OperationResult } from '../types/operations';
import { postOperation } from './operationClient';

export const startFlash = async (sessionId: string, file: File): Promise<OperationResult> =>
    postOperation(`/sessions/${sessionId}/flash`, file);