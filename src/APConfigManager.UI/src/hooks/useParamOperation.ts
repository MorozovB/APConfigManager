import { useState, useCallback } from 'react';
import { OperationResult, Parameter } from '../types/operations';
import { uploadParams, readParams, resetParams } from '../api/paramsApi';

export const useParamOperation = () => {
    const [isRunning, setIsRunning] = useState(false);
    const [result, setResult] = useState<OperationResult | null>(null);
    const [parameters, setParameters] = useState<Parameter[]>([]);
    const [error, setError] = useState<string | null>(null);

    const upload = useCallback(async (sessionId: string, file: File) => {
        setIsRunning(true);
        setResult(null);
        setError(null);

        try {
            const data = await uploadParams(sessionId, file);
            setResult(data);
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Upload failed');
        } finally {
            setIsRunning(false);
        }
    }, []);

    const download = useCallback(async (sessionId: string) => {
        setIsRunning(true);
        setError(null);

        try {
            const data = await readParams(sessionId);
            setParameters(data);
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Download failed');
        } finally {
            setIsRunning(false);
        }
    }, []);

    const reset = useCallback(async (sessionId: string) => {
        setIsRunning(true);
        setResult(null);
        setError(null);

        try {
            const data = await resetParams(sessionId);
            setResult(data);
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Reset failed');
        } finally {
            setIsRunning(false);
        }
    }, []);

    const clear = useCallback(() => {
        setResult(null);
        setError(null);
        setParameters([]);
    }, []);

    return { isRunning, result, parameters, error, upload, download, reset, clear };
};
