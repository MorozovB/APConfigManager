import { useState, useCallback } from 'react';
import { OperationResult } from '../types/operations';
import { startErase } from '../api/eraseApi';

export const useEraseOperation = () => {
    const [isRunning, setIsRunning] = useState(false);
    const [result, setResult] = useState<OperationResult | null>(null);
    const [error, setError] = useState<string | null>(null);

    const execute = useCallback(async (sessionId: string) => {
        setIsRunning(true);
        setResult(null);
        setError(null);

        try {
            const data = await startErase(sessionId);
            setResult(data);
            return data;
        } catch (err) {
            const message = err instanceof Error ? err.message : 'Erase failed';
            setError(message);
            return null;
        } finally {
            setIsRunning(false);
        }
    }, []);

    const reset = useCallback(() => {
        setResult(null);
        setError(null);
    }, []);

    return { isRunning, result, error, execute, reset };
};
