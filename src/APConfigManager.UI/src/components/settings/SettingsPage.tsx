import { useState } from 'react';
import {
    Text,
    Dropdown,
    Option,
    Switch,
    Button,
    Card,
    Field,
} from '@fluentui/react-components';
import { SaveRegular } from '@fluentui/react-icons';
import { useTranslation } from 'react-i18next';
import { useSettings } from '../../hooks/useSettings.ts';
import { useThemeMode } from '../../contexts/ThemeModeContext';

const languages = [
    { code: 'EN', label: 'English' },
    { code: 'CZ', label: 'Čeština' },
    { code: 'UA', label: 'Українська' },
];

// Standard serial speeds; capped so a user can't pick an unusably high value.
const baudRates = [57600, 115200, 230400, 460800, 921600];

export const SettingsPage = () => {
    const { t, i18n } = useTranslation();
    const { settings, loading, error, save } = useSettings();
    const { mode, setMode } = useThemeMode();

    const [languageOverride, setLanguageOverride] = useState<string | null>(null);
    const language = languageOverride ?? settings?.language ?? 'UA';

    const [startupOverride, setStartupOverride] = useState<number | null>(null);
    const startupSessions = startupOverride ?? settings?.startupSessions ?? 1;
    const [baudRateOverride, setBaudRateOverride] = useState<number | null>(null);
    const portBaudRate = baudRateOverride ?? settings?.portBaudRate ?? 115200;

    const [saved, setSaved] = useState(false);

    const handleLanguageSelect = async (code: string) => {
        setLanguageOverride(code);
        localStorage.setItem('lang', code);
        await i18n.changeLanguage(code);
    };

    const handleSave = async () => {
        await save({ language, theme: mode, startupSessions, portBaudRate });
        setSaved(true);
        setTimeout(() => setSaved(false), 3000);
    };

    if (loading) {
        return (
            <Text size={200} style={{ color: 'var(--colorNeutralForeground3)' }}>
                {t('settings.loading')}
            </Text>
        );
    }

    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '16px', maxWidth: '500px' }}>

            <Text size={500} weight="semibold">{t('settings.title')}</Text>

            {error && (
                <Text size={200} style={{ color: '#ff7675' }}>{error}</Text>
            )}

            <Card style={{
                padding: '20px',
                backgroundColor: 'var(--colorNeutralBackground2)',
                border: '1px solid var(--colorNeutralStroke1)',
                display: 'flex',
                flexDirection: 'column',
                gap: '20px',
            }}>

                <Field label={t('settings.language')}>
                    <Dropdown
                        value={languages.find(l => l.code === language)?.label || ''}
                        selectedOptions={[language]}
                        onOptionSelect={(_e, data) => {
                            if (data.optionValue) handleLanguageSelect(data.optionValue);   // ← НЕ setLanguageOverride
                        }}
                    >
                        {languages.map((lang) => (
                            <Option key={lang.code} value={lang.code} text={lang.label}>
                                {lang.label}
                            </Option>
                        ))}
                    </Dropdown>
                </Field>

                <Field label={t('settings.theme')}>
                    <Switch
                        checked={mode === 'dark'}
                        onChange={(_e, data) => setMode(data.checked ? 'dark' : 'light')}
                        label={mode === 'dark' ? t('settings.dark') : t('settings.light')}
                    />
                </Field>

                <Field label={t('settings.startupSessions')}>
                    <Dropdown
                        value={String(startupSessions)}
                        selectedOptions={[String(startupSessions)]}
                        onOptionSelect={(_e, data) => {
                            if (data.optionValue) setStartupOverride(Number(data.optionValue));
                        }}
                    >
                        {[1, 2, 3, 4, 5, 6, 7].map((n) => (
                            <Option key={n} value={String(n)} text={String(n)}>{n}</Option>
                        ))}
                    </Dropdown>
                </Field>

                <Field label={t('settings.portBaudRate')}>
                    <Dropdown
                        value={String(portBaudRate)}
                        selectedOptions={[String(portBaudRate)]}
                        onOptionSelect={(_e, data) => {
                            if (data.optionValue) setBaudRateOverride(Number(data.optionValue));
                        }}
                    >
                        {baudRates.map((r) => (
                            <Option key={r} value={String(r)} text={String(r)}>{r}</Option>
                        ))}
                    </Dropdown>
                </Field>

                <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                    <Button appearance="primary" icon={<SaveRegular />} onClick={handleSave}>
                        {t('settings.save')}
                    </Button>

                    {saved && (
                        <Text size={200} style={{ color: '#00b894' }}>
                            {t('settings.saved')}
                        </Text>
                    )}
                </div>
            </Card>
        </div>
    );
};