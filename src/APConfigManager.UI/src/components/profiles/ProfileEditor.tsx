import { useState, useEffect, useRef } from 'react';
import {
  Dialog,
  DialogSurface,
  DialogBody,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Input,
  Checkbox,
  Field,
  Textarea,
  Text,
} from '@fluentui/react-components';

import { DeviceProfile } from '../../types/profile';
import {
  uploadProfileFirmware,
  uploadProfileParameters,
} from '../../api/profileFilesApi';

interface Props {
  open: boolean;
  profile: DeviceProfile | null;
  onSave: (profile: DeviceProfile) => void;
  onCancel: () => void;
}

const emptyProfile: DeviceProfile = {
  id: '',
  name: '',
  description: '',
  boardType: 0,
  parameterFilePath: null,
  firmwareFilePath: null,
  parameterFileName: null,
  firmwareFileName: null,
  profileOptions: {
    bootloader: false,
    firmware: false,
    parameters: false,
  },
};

const createFormState = (profile: DeviceProfile | null): DeviceProfile => {
  if (profile) {
    return { ...profile };
  }
  return { ...emptyProfile };
};

export const ProfileEditor = ({
  open,
  profile,
  onSave,
  onCancel,
}: Props) => {
  const [form, setForm] = useState<DeviceProfile>(() => createFormState(profile));
  const [uploadingFirmware, setUploadingFirmware] = useState(false);
  const [uploadingParams, setUploadingParams] = useState(false);
  const [uploadError, setUploadError] = useState<string | null>(null);

  const profileIdRef = useRef<string>(profile?.id || crypto.randomUUID());

  useEffect(() => {
    if (!open) {
      return;
    }

    profileIdRef.current = profile?.id ?? crypto.randomUUID();
    setForm(createFormState(profile));
    setUploadError(null);
  }, [profile, open]);

  const handleFieldChange = (
    field: keyof DeviceProfile,
    value: string | number,
  ) => {
    setForm(prev => ({
      ...prev,
      [field]: value,
    }));
  };

  const handlePathChange = (
    field: 'firmwareFilePath' | 'parameterFilePath',
    value: string,
  ) => {
    setForm(prev => ({
      ...prev,
      [field]: value.trim() ? value : null,
    }));
  };

  const handleOptionChange = (option: string, checked: boolean) => {
    setForm(prev => ({
      ...prev,
      profileOptions: {
        ...prev.profileOptions,
        [option]: checked,
      },
    }));
  };

  const handleFirmwareFile = async (
    e: React.ChangeEvent<HTMLInputElement>,
  ) => {
    const file = e.target.files?.[0];
    if (!file) {
      return;
    }

    setUploadingFirmware(true);
    setUploadError(null);

    try {
      const path = await uploadProfileFirmware(profileIdRef.current, file);
      setForm(prev => ({
        ...prev,
        firmwareFilePath: path,
      }));
    } catch (err) {
      setUploadError(
        err instanceof Error ? err.message : 'Failed to upload firmware file',
      );
    } finally {
      setUploadingFirmware(false);
      e.target.value = '';
    }
  };

  const handleParamFile = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) {
      return;
    }

    setUploadingParams(true);
    setUploadError(null);

    try {
      const path = await uploadProfileParameters(profileIdRef.current, file);
      setForm(prev => ({
        ...prev,
        parameterFilePath: path,
      }));
    } catch (err) {
      setUploadError(
        err instanceof Error ? err.message : 'Failed to upload parameter file',
      );
    } finally {
      setUploadingParams(false);
      e.target.value = '';
    }
  };

  const handleSave = () => {
    const id = profile?.id ?? profileIdRef.current;

    const profileToSave: DeviceProfile = {
      ...form,
      id,
      description: form.description || '',
      firmwareFilePath: form.firmwareFilePath?.trim() || null,
      parameterFilePath: form.parameterFilePath?.trim() || null,
    };

    console.log('ProfileEditor save:', JSON.stringify(profileToSave, null, 2));

    onSave(profileToSave);
  };

  const isValid = form.name.trim().length > 0;

  const showBootloaderWarning =
    form.profileOptions.bootloader && !form.profileOptions.firmware;

  return (
    <Dialog
      open={open}
      onOpenChange={(_e, data) => {
        if (!data.open) {
          onCancel();
        }
      }}
    >
      <DialogSurface style={{ maxWidth: '560px' }}>
        <DialogBody>
          <DialogTitle>
            {profile ? 'Edit Profile' : 'New Profile'}
          </DialogTitle>

          <DialogContent>
            <div
              style={{
                display: 'flex',
                flexDirection: 'column',
                gap: '16px',
                paddingTop: '8px',
              }}
            >
              <Field label="Profile name" required>
                <Input
                  value={form.name}
                  onChange={(_e, data) => handleFieldChange('name', data.value)}
                  placeholder="e.g. CubeOrange Copter"
                />
              </Field>

              <Field label="Description">
                <Textarea
                  value={form.description}
                  onChange={(_e, data) =>
                    handleFieldChange('description', data.value)
                  }
                  placeholder="Optional description"
                  rows={2}
                />
              </Field>

              <Field
                label="Firmware file (.apj)"
                hint="Full path on this PC, or use Browse to copy into app storage"
              >
                <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                  <Input
                    value={form.firmwareFilePath || ''}
                    onChange={(_e, data) =>
                      handlePathChange('firmwareFilePath', data.value)
                    }
                    placeholder="C:\Firmware\copter.apj"
                    style={{ flex: 1 }}
                  />
                  <label style={{ cursor: 'pointer' }}>
                    <input
                      type="file"
                      accept=".apj"
                      onChange={handleFirmwareFile}
                      style={{ display: 'none' }}
                      disabled={uploadingFirmware}
                    />
                    <Button
                      size="small"
                      appearance="outline"
                      disabled={uploadingFirmware}
                      onClick={e => {
                        const input = (e.currentTarget as HTMLElement)
                          .parentElement?.querySelector('input');
                        input?.click();
                      }}
                    >
                      {uploadingFirmware ? 'Uploading...' : 'Browse'}
                    </Button>
                  </label>
                </div>
              </Field>

              <Field
                label="Parameters file (.param)"
                hint="Full path on this PC, or use Browse to copy into app storage"
              >
                <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                  <Input
                    value={form.parameterFilePath || ''}
                    onChange={(_e, data) =>
                      handlePathChange('parameterFilePath', data.value)
                    }
                    placeholder="C:\Params\default.param"
                    style={{ flex: 1 }}
                  />
                  <label style={{ cursor: 'pointer' }}>
                    <input
                      type="file"
                      accept=".param"
                      onChange={handleParamFile}
                      style={{ display: 'none' }}
                      disabled={uploadingParams}
                    />
                    <Button
                      size="small"
                      appearance="outline"
                      disabled={uploadingParams}
                      onClick={e => {
                        const input = (e.currentTarget as HTMLElement)
                          .parentElement?.querySelector('input');
                        input?.click();
                      }}
                    >
                      {uploadingParams ? 'Uploading...' : 'Browse'}
                    </Button>
                  </label>
                </div>
              </Field>

              {uploadError && (
                <Text size={200} style={{ color: '#ff7675' }}>
                  {uploadError}
                </Text>
              )}

              <Field label="Operations to perform">
                <div
                  style={{
                    display: 'flex',
                    flexDirection: 'column',
                    gap: '8px',
                    paddingTop: '4px',
                  }}
                >
                  <Checkbox
                    label="Flash firmware"
                    checked={form.profileOptions.firmware || false}
                    onChange={(_e, data) =>
                      handleOptionChange('firmware', !!data.checked)
                    }
                  />
                  <div>
                    <Checkbox
                      label="Update bootloader"
                      checked={form.profileOptions.bootloader || false}
                      onChange={(_e, data) =>
                        handleOptionChange('bootloader', !!data.checked)
                      }
                    />
                    {showBootloaderWarning && (
                      <Text
                        size={200}
                        style={{
                          color: '#fdcb6e',
                          display: 'block',
                          marginLeft: '28px',
                          marginTop: '2px',
                        }}
                      >
                        Bootloader update requires firmware to be installed on the device.
                      </Text>
                    )}
                  </div>
                  <Checkbox
                    label="Upload parameters"
                    checked={form.profileOptions.parameters || false}
                    onChange={(_e, data) =>
                      handleOptionChange('parameters', !!data.checked)
                    }
                  />
                </div>
              </Field>
            </div>
          </DialogContent>

          <DialogActions>
            <Button appearance="secondary" onClick={onCancel}>
              Cancel
            </Button>
            <Button
              appearance="primary"
              onClick={handleSave}
              disabled={!isValid || uploadingFirmware || uploadingParams}
            >
              {profile ? 'Save' : 'Create'}
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
};
