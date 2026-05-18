import { useState, useCallback } from 'react';

export interface ProfileFiles {
  firmwareFile: File | null;
  paramFile: File | null;
}

const fileStore = new Map<string, ProfileFiles>();

export const useProfileFiles = () => {
  const [, forceUpdate] = useState(0);

  const setFiles = useCallback((profileId: string, files: ProfileFiles) => {
    fileStore.set(profileId, files);
    forceUpdate(n => n + 1);
  }, []);

  const getFiles = useCallback((profileId: string): ProfileFiles => {
    return fileStore.get(profileId) || { firmwareFile: null, paramFile: null };
  }, []);

  const setFirmwareFile = useCallback((profileId: string, file: File | null) => {
    const current = fileStore.get(profileId) || { firmwareFile: null, paramFile: null };
    fileStore.set(profileId, { ...current, firmwareFile: file });
    forceUpdate(n => n + 1);
  }, []);

  const setParamFile = useCallback((profileId: string, file: File | null) => {
    const current = fileStore.get(profileId) || { firmwareFile: null, paramFile: null };
    fileStore.set(profileId, { ...current, paramFile: file });
    forceUpdate(n => n + 1);
  }, []);

  return { getFiles, setFiles, setFirmwareFile, setParamFile };
};
