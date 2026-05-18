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
import { useProfileFiles } from '../../hooks/useProfileFiles';

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
  profileOptions: {
    bootloader: false,
    firmware: false,
    parameters: false,
  },
};

interface FormState {
  data: DeviceProfile;
  firmwareFileName: string;
  paramFileName: string;
}

export const ProfileEditor = ({ open, profile, onSave, onCancel }: Props) => {
  const [form, setForm] = useState<FormState>({
    data: { ...emptyProfile },
    firmwareFileName: '',
    paramFileName: '',
  });

  const { setFirmwareFile: storeFirmwareFile, setParamFile: storeParamFile, getFiles } = useProfileFiles();

  const firmwareFileRef = useRef<File | null>(null);
  const paramFileRef = useRef<File | null>(null);

  // eslint-disable-next-line react-hooks/set-state-in-effect
  useEffect(() => {
    if (!open) return;

    if (profile) {
      const files = getFiles(profile.id);
      firmwareFileRef.current = files.firmwareFile;
      paramFileRef.current = files.paramFile;
      setForm({
        data: { ...profile },
        firmwareFileName: profile.firmwareFilePath || '',
        paramFileName: profile.parameterFilePath || '',
      });
    } else {
      firmwareFileRef.current = null;
      paramFileRef.current = null;
      setForm({
        data: { ...emptyProfile },
        firmwareFileName: '',
        paramFileName: '',
      });
    }
  }, [profile, open, getFiles]);

  const handleFieldChange = (field: keyof DeviceProfile, value: string | number) => {
    setForm(prev => ({
      ...prev,
      data: { ...prev.data, [field]: value },
    }));
  };

  const handleOptionChange = (option: string, checked: boolean) => {
    setForm(prev => ({
      ...prev,
      data: {
        ...prev.data,
        profileOptions: { ...prev.data.profileOptions, [option]: checked },
      },
    }));
  };

  const handleFirmwareFile = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      firmwareFileRef.current = file;
      setForm(prev => ({
        ...prev,
        firmwareFileName: file.name,
        data: { ...prev.data, firmwareFilePath: file.name },
      }));
    }
  };

  const handleParamFile = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      paramFileRef.current = file;
      setForm(prev => ({
        ...prev,
        paramFileName: file.name,
        data: { ...prev.data, parameterFilePath: file.name },
      }));
    }
  };

  const handleSave = () => {
    const id = form.data.id || crypto.randomUUID();
    const profileToSave: DeviceProfile = { ...form.data, id };

    if (firmwareFileRef.current) {
      storeFirmwareFile(id, firmwareFileRef.current);
    }
    if (paramFileRef.current) {
      storeParamFile(id, paramFileRef.current);
    }

    onSave(profileToSave);
  };

  const isValid = form.data.name.trim().length > 0;

  const showBootloaderWarning =
    form.data.profileOptions.bootloader && !form.data.profileOptions.firmware;

  return (
    <Dialog open={open} onOpenChange={(_e, data) => { if (!data.open) onCancel(); }}>
      <DialogSurface style={{ maxWidth: '500px' }}>
        <DialogBody>
          <DialogTitle>
            {profile ? 'Edit Profile' : 'New Profile'}
          </DialogTitle>

          <DialogContent>
            <div style={{ display: 'flex', flexDirection: 'column', gap: '16px', paddingTop: '8px' }}>

              <Field label="Profile name" required>
                <Input
                  value={form.data.name}
                  onChange={(_e, data) => handleFieldChange('name', data.value)}
                  placeholder="e.g. CubeOrange Copter"
                />
              </Field>

              <Field label="Description">
                <Textarea
                  value={form.data.description}
                  onChange={(_e, data) => handleFieldChange('description', data.value)}
                  placeholder="Optional description"
                  rows={2}
                />
              </Field>

              <Field label="Firmware file (.apj)">
                <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                  <Input
                    value={form.firmwareFileName}
                    readOnly
                    placeholder="No file selected"
                    style={{ flex: 1 }}
                  />
                  <label style={{ cursor: 'pointer' }}>
                    <input
                      type="file"
                      accept=".apj"
                      onChange={handleFirmwareFile}
                      style={{ display: 'none' }}
                    />
                    <Button
                      size="small"
                      appearance="outline"
                      onClick={(e) => {
                        const input = (e.currentTarget as HTMLElement).parentElement?.querySelector('input');
                        input?.click();
                      }}
                    >
                      Browse
                    </Button>
                  </label>
                </div>
              </Field>

              <Field label="Parameters file (.param)">
                <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                  <Input
                    value={form.paramFileName}
                    readOnly
                    placeholder="No file selected"
                    style={{ flex: 1 }}
                  />
                  <label style={{ cursor: 'pointer' }}>
                    <input
                      type="file"
                      accept=".param"
                      onChange={handleParamFile}
                      style={{ display: 'none' }}
                    />
                    <Button
                      size="small"
                      appearance="outline"
                      onClick={(e) => {
                        const input = (e.currentTarget as HTMLElement).parentElement?.querySelector('input');
                        input?.click();
                      }}
                    >
                      Browse
                    </Button>
                  </label>
                </div>
              </Field>

              <Field label="Operations to perform">
                <div style={{ display: 'flex', flexDirection: 'column', gap: '8px', paddingTop: '4px' }}>
                  <Checkbox
                    label="Flash firmware"
                    checked={form.data.profileOptions.firmware || false}
                    onChange={(_e, data) => handleOptionChange('firmware', !!data.checked)}
                  />
                  <div>
                    <Checkbox
                      label="Update bootloader"
                      checked={form.data.profileOptions.bootloader || false}
                      onChange={(_e, data) => handleOptionChange('bootloader', !!data.checked)}
                    />
                    {showBootloaderWarning && (
                      <Text size={200} style={{ color: '#fdcb6e', display: 'block', marginLeft: '28px', marginTop: '2px' }}>
                        ⚠ Bootloader update requires firmware to be installed on the device.
                      </Text>
                    )}
                  </div>
                  <Checkbox
                    label="Upload parameters"
                    checked={form.data.profileOptions.parameters || false}
                    onChange={(_e, data) => handleOptionChange('parameters', !!data.checked)}
                  />
                </div>
              </Field>

            </div>
          </DialogContent>

          <DialogActions>
            <Button appearance="secondary" onClick={onCancel}>
              Cancel
            </Button>
            <Button appearance="primary" onClick={handleSave} disabled={!isValid}>
              {profile ? 'Save' : 'Create'}
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
};
