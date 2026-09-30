import {useState, useEffect, useRef} from 'react';
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

import {DeviceProfile} from '../../types/profile';
import {useTranslation} from "react-i18next";
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
        return {...profile};
    }
    return {...emptyProfile};
};

export const ProfileEditor = ({
                                  open,
                                  profile,
                                  onSave,
                                  onCancel,
                              }: Props) => {
    const {t} = useTranslation();
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
                err instanceof Error ? err.message : t('profiles.uploadFirmwareError'),
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
                err instanceof Error ? err.message : t('profiles.uploadParameterError'),
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
            <DialogSurface style={{maxWidth: '560px'}}>
                <DialogBody>
                    <DialogTitle>
                        {profile ? t('profiles.editProfile') : t('profiles.newProfile')}
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
                            <Field label={t('profiles.name')} required>
                                <Input
                                    value={form.name}
                                    onChange={(_e, data) => handleFieldChange('name', data.value)}
                                    placeholder={t('profiles.namePlaceholder')}
                                />
                            </Field>

                            <Field label={t('profiles.description')}>
                                <Textarea
                                    value={form.description}
                                    onChange={(_e, data) =>
                                        handleFieldChange('description', data.value)
                                    }
                                    placeholder={t('profiles.descriptionPlaceholder')}
                                    rows={2}
                                />
                            </Field>

                            <Field
                                label={t('profiles.firmwareFile')}
                                hint={t('profiles.pathHint')}
                            >
                                <div style={{display: 'flex', gap: '8px', alignItems: 'center'}}>
                                    <Input
                                        value={form.firmwareFilePath || ''}
                                        onChange={(_e, data) =>
                                            handlePathChange('firmwareFilePath', data.value)
                                        }
                                        placeholder={t('profiles.firmwarePathPlaceholder')}
                                        style={{flex: 1}}
                                    />
                                    <label style={{cursor: 'pointer'}}>
                                        <input
                                            type="file"
                                            accept=".apj"
                                            onChange={handleFirmwareFile}
                                            style={{display: 'none'}}
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
                                            {uploadingFirmware ? t('common.uploading') : t('common.browse')}
                                        </Button>
                                    </label>
                                </div>
                            </Field>

                            <Field
                                label={t('profiles.parametersFile')}
                                hint={t('profiles.pathHint')}
                            >
                                <div style={{display: 'flex', gap: '8px', alignItems: 'center'}}>
                                    <Input
                                        value={form.parameterFilePath || ''}
                                        onChange={(_e, data) =>
                                            handlePathChange('parameterFilePath', data.value)
                                        }
                                        placeholder={t('profiles.parametersPathPlaceholder')}
                                        style={{flex: 1}}
                                    />
                                    <label style={{cursor: 'pointer'}}>
                                        <input
                                            type="file"
                                            accept=".param"
                                            onChange={handleParamFile}
                                            style={{display: 'none'}}
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
                                            {uploadingParams ? t('common.uploading') : t('common.browse')}
                                        </Button>
                                    </label>
                                </div>
                            </Field>

                            {uploadError && (
                                <Text size={200} style={{color: '#ff7675'}}>
                                    {uploadError}
                                </Text>
                            )}

                            <Field label={t('profiles.operationsToPerform')}>
                                <div
                                    style={{
                                        display: 'flex',
                                        flexDirection: 'column',
                                        gap: '8px',
                                        paddingTop: '4px',
                                    }}
                                >
                                    <Checkbox
                                        label={t('profiles.flashFirmware')}
                                        checked={form.profileOptions.firmware || false}
                                        onChange={(_e, data) =>
                                            handleOptionChange('firmware', !!data.checked)
                                        }
                                    />
                                    <div>
                                        <Checkbox
                                            label={t('profiles.updateBootloader')}
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
                                                {t('profiles.bootloaderRequiresFirmware')}
                                            </Text>
                                        )}
                                    </div>
                                    <Checkbox
                                        label={t('profiles.uploadParameters')}
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
                            {t('common.cancel')}
                        </Button>
                        <Button
                            appearance="primary"
                            onClick={handleSave}
                            disabled={!isValid || uploadingFirmware || uploadingParams}
                        >
                            {profile ? t('common.save') : t('common.create')}
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};
