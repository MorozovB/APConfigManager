import { useState, useCallback } from 'react';
import { OperationResult } from '../types/operations';
import { startFlash } from '../api/flashApi';

export const useFlashOperation = () => {
  const [isRunning, setIsRunning] = useState(false);
  const [result, setResult] = useState<OperationResult | null>(null);
  const [error, setError] = useState<string | null>(null);

  const execute = useCallback(async (sessionId: string, file: File) => {
    setIsRunning(true);
    setResult(null);
    setError(null);

    try {
      const data = await startFlash(sessionId, file);
      setResult(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Flash failed');
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
