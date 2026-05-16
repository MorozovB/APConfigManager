import { useState, useEffect } from 'react';
import {
  Dialog,
  DialogSurface,
  DialogBody,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Input,
  Label,
  Checkbox,
  Field,
  Textarea,
} from '@fluentui/react-components';
import { DeviceProfile } from '../../types/profile';

interface Props {
  open: boolean;
  profile: DeviceProfile | null;   // null = создание нового, объект = редактирование
  onSave: (profile: DeviceProfile) => void;
  onCancel: () => void;
}

// Пустой профиль для формы создания
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

export const ProfileEditor = ({ open, profile, onSave, onCancel }: Props) => {
  const [formData, setFormData] = useState<DeviceProfile>(emptyProfile);

  const [firmwareFileName, setFirmwareFileName] = useState('');
  const [paramFileName, setParamFileName] = useState('');

  useEffect(() => {
    if (open) {
      if (profile) {
        setFormData({ ...profile });
        setFirmwareFileName(profile.firmwareFilePath || '');
        setParamFileName(profile.parameterFilePath || '');
      } else {
        setFormData({ ...emptyProfile });
        setFirmwareFileName('');
        setParamFileName('');
      }
    }
  }, [profile, open]);

  const handleFieldChange = (field: keyof DeviceProfile, value: string | number) => {
    setFormData(prev => ({ ...prev, [field]: value }));
  };

  const handleOptionChange = (option: string, checked: boolean) => {
    setFormData(prev => ({
      ...prev,
      profileOptions: { ...prev.profileOptions, [option]: checked },
    }));
  };

  const handleFirmwareFile = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      setFirmwareFileName(file.name);
      setFormData(prev => ({ ...prev, firmwareFilePath: file.name }));
    }
  };

  const handleParamFile = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      setParamFileName(file.name);
      setFormData(prev => ({ ...prev, parameterFilePath: file.name }));
    }
  };

  const handleSave = () => {
    const profileToSave: DeviceProfile = {
      ...formData,
      id: formData.id || crypto.randomUUID(),
    };
    onSave(profileToSave);
  };

  const isValid = formData.name.trim().length > 0;

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
                  value={formData.name}
                  onChange={(_e, data) => handleFieldChange('name', data.value)}
                  placeholder="e.g. CubeOrange Copter"
                />
              </Field>

              <Field label="Description">
                <Textarea
                  value={formData.description}
                  onChange={(_e, data) => handleFieldChange('description', data.value)}
                  placeholder="Optional description"
                  rows={2}
                />
              </Field>

              <Field label="Firmware file (.apj)">
                <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                  <Input
                    value={firmwareFileName}
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
                    value={paramFileName}
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
                    label="Update bootloader"
                    checked={formData.profileOptions.bootloader || false}
                    onChange={(_e, data) => handleOptionChange('bootloader', !!data.checked)}
                  />
                  <Checkbox
                    label="Flash firmware"
                    checked={formData.profileOptions.firmware || false}
                    onChange={(_e, data) => handleOptionChange('firmware', !!data.checked)}
                  />
                  <Checkbox
                    label="Upload parameters"
                    checked={formData.profileOptions.parameters || false}
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
