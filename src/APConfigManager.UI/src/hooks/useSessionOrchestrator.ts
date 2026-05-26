import { useState, useCallback } from 'react';
import { DeviceProfile } from '../types/profile';
import { startFlash } from '../api/flashApi';
import { uploadParams } from '../api/paramsApi';
import { updateBootloader } from '../api/bootApi';

export type OrchestratorStage =
  | 'idle'
  | 'flashing'
  | 'bootloader'
  | 'params'
  | 'done'
  | 'error';

export interface StageResult {
  stage: OrchestratorStage;
  success: boolean;
  message: string;
}

export const useSessionOrchestrator = () => {
  const [stage, setStage] = useState<OrchestratorStage>('idle');
  const [error, setError] = useState<string | null>(null);
  const [isRunning, setIsRunning] = useState(false);
  const [results, setResults] = useState<StageResult[]>([]);

  const addResult = (stageResult: StageResult) => {
    setResults(prev => [...prev, stageResult]);
  };

  const start = useCallback(async (
    sessionId: string,
    profile: DeviceProfile,
    firmwareFile: File | null,
    paramFile: File | null,
    refreshSession: () => Promise<void>
  ) => {
    setIsRunning(true);
    setError(null);
    setResults([]);

    console.log('Orchestrator start:', {
      sessionId,
      profileName: profile.name,
      options: profile.profileOptions,
      hasFirmwareFile: firmwareFile !== null,
      firmwareFileName: firmwareFile?.name,
      hasParamFile: paramFile !== null,
      paramFileName: paramFile?.name,
    });

    try {
      if (profile.profileOptions?.firmware && firmwareFile) {
        setStage('flashing');
        const flashResult = await startFlash(sessionId, firmwareFile);
        if (!flashResult.success) {
          const msg = flashResult.message || 'Flash failed';
          addResult({ stage: 'flashing', success: false, message: msg });
          throw new Error(msg);
        }
        addResult({ stage: 'flashing', success: true, message: 'Firmware flashed successfully' });
        await refreshSession();
      }

      if (profile.profileOptions?.bootloader) {
        setStage('bootloader');
        const blResult = await updateBootloader(sessionId);
        if (!blResult.success) {
          const msg = blResult.message || 'Bootloader update failed';
          addResult({ stage: 'bootloader', success: false, message: msg });
          throw new Error(msg);
        }
        addResult({ stage: 'bootloader', success: true, message: 'Bootloader updated successfully' });
        await refreshSession();
      }

      if (profile.profileOptions?.parameters && paramFile) {
        setStage('params');
        const paramResult = await uploadParams(sessionId, paramFile);
        if (!paramResult.success) {
          const msg = paramResult.message || 'Parameter upload failed';
          addResult({ stage: 'params', success: false, message: msg });
          throw new Error(msg);
        }
        addResult({ stage: 'params', success: true, message: 'Parameters uploaded' });
        await refreshSession();
      }

      setStage('done');
      addResult({ stage: 'done', success: true, message: 'All operations completed' });
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
  }, []);

  const reset = useCallback(() => {
    setStage('idle');
    setError(null);
    setIsRunning(false);
    setResults([]);
  }, []);

  return { stage, error, isRunning, results, start, stop, reset };
};
