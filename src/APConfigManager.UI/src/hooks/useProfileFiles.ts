import { useState, useCallback } from 'react';
import { DeviceProfile } from '../types/profile';
import {
  fetchProfileFirmware,
  fetchProfileParameters,
  blobToFile,
  isAbsolutePath,
} from '../api/profileFilesApi';

export interface ProfileFiles {
  firmwareFile: File | null;
  paramFile: File | null;
}

const fileStore = new Map<string, ProfileFiles>();

const filenameOnlyHint = (storedPath: string): string =>
  `Profile stores only the file name "${storedPath}". ` +
  'Edit the profile: use Browse or enter a full path (e.g. D:\\Firmware\\file.apj), then Save.';

export const useProfileFiles = () => {
  const [, forceUpdate] = useState(0);

  const setFiles = useCallback((profileId: string, files: ProfileFiles) => {
    fileStore.set(profileId, files);
    forceUpdate(n => n + 1);
  }, []);

  const getFiles = useCallback((profileId: string): ProfileFiles => {
    return fileStore.get(profileId) || { firmwareFile: null, paramFile: null };
  }, []);

  const loadFromServer = useCallback(async (profile: DeviceProfile): Promise<ProfileFiles> => {
    const files: ProfileFiles = { firmwareFile: null, paramFile: null };
    const errors: string[] = [];

    const firmwarePath = profile.firmwareFilePath?.trim();
    const paramPath = profile.parameterFilePath?.trim();

    if (profile.profileOptions?.firmware && firmwarePath) {
      try {
        const blob = await fetchProfileFirmware(profile.id);
        files.firmwareFile = blobToFile(blob, firmwarePath);
      } catch (err) {
        const message = err instanceof Error ? err.message : 'Firmware load failed';
        errors.push(
          !isAbsolutePath(firmwarePath)
            ? `${message}. ${filenameOnlyHint(firmwarePath)}`
            : message,
        );
      }
    }

    if (profile.profileOptions?.parameters && paramPath) {
      try {
        const blob = await fetchProfileParameters(profile.id);
        files.paramFile = blobToFile(blob, paramPath);
      } catch (err) {
        const message = err instanceof Error ? err.message : 'Parameters load failed';
        errors.push(
          !isAbsolutePath(paramPath)
            ? `${message}. ${filenameOnlyHint(paramPath)}`
            : message,
        );
      }
    }

    fileStore.set(profile.id, files);
    forceUpdate(n => n + 1);

    if (errors.length > 0) {
      throw new Error(errors.join(' | '));
    }

    return files;
  }, []);

  const clearFiles = useCallback((profileId: string) => {
    fileStore.delete(profileId);
    forceUpdate(n => n + 1);
  }, []);

  return { getFiles, setFiles, loadFromServer, clearFiles };
};
