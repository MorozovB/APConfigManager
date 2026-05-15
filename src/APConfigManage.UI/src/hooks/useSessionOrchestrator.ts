import { useState, useCallback } from 'react';
import { DeviceProfile } from '../types/profile';
import { startFlash } from '../api/flashApi';
import { uploadParams } from '../api/paramsApi';

// Статусы автоматического процесса
export type OrchestratorStage =
  | 'idle'
  | 'bootloader'
  | 'flashing'
  | 'params'
  | 'done'
  | 'error';

export const useSessionOrchestrator = () => {
  const [stage, setStage] = useState<OrchestratorStage>('idle');
  const [error, setError] = useState<string | null>(null);
  const [isRunning, setIsRunning] = useState(false);


  const start = useCallback(async (
    sessionId: string,
    profile: DeviceProfile,
    firmwareFile: File | null,
    paramFile: File | null
  ) => {
    setIsRunning(true);
    setError(null);

    try {
      if (profile.profileOptions?.bootloader) {
        setStage('bootloader');
        // TODO: реализовать обновление bootloader
        console.log('Bootloader update — not implemented yet');
      }

      if (profile.profileOptions?.firmware && firmwareFile) {
        setStage('flashing');
        const flashResult = await startFlash(sessionId, firmwareFile);
        if (!flashResult.success) {
          throw new Error(flashResult.message || 'Flash failed');
        }
      }

      if (profile.profileOptions?.parameters && paramFile) {
        setStage('params');
        const paramResult = await uploadParams(sessionId, paramFile);
        if (!paramResult.success) {
          throw new Error(paramResult.message || 'Parameter upload failed');
        }
      }

      setStage('done');
    } catch (err) {
      setStage('error');
      setError(err instanceof Error ? err.message : 'Process failed');
    } finally {
      setIsRunning(false);
    }
  }, []);

  const stop = useCallback(() => {
    setStage('idle');
    setError(null);
    setIsRunning(false);
    // TODO: отправить CancellationToken на backend
  }, []);

  const reset = useCallback(() => {
    setStage('idle');
    setError(null);
    setIsRunning(false);
  }, []);

  return { stage, error, isRunning, start, stop, reset };
};
