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
import { useSettings } from '../../hooks/useSettings.ts';

const languages = [
  { code: 'UA', label: 'Українська' },
  { code: 'EN', label: 'English' },
  { code: 'RU', label: 'Русский' },
];

export const SettingsPage = () => {
  const { settings, loading, error, save } = useSettings();

  const [language, setLanguage] = useState(settings?.language || 'UA');
  const [darkMode, setDarkMode] = useState(true);
  const [saved, setSaved] = useState(false);

  const handleSave = async () => {
    await save({ language });
    setSaved(true);
    setTimeout(() => setSaved(false), 3000);
  };

  if (loading) {
    return (
      <Text size={200} style={{ color: 'var(--colorNeutralForeground3)' }}>
        Loading settings...
      </Text>
    );
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '16px', maxWidth: '500px' }}>

      <Text size={500} weight="semibold">Settings</Text>

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

        <Field label="Language">
          <Dropdown
            value={languages.find(l => l.code === language)?.label || ''}
            selectedOptions={[language]}
            onOptionSelect={(_e, data) => {
              if (data.optionValue) setLanguage(data.optionValue);
            }}
          >
            {languages.map((lang) => (
              <Option key={lang.code} value={lang.code} text={lang.label}>
                {lang.label}
              </Option>
            ))}
          </Dropdown>
        </Field>

        <Field label="Theme">
          <Switch
            checked={darkMode}
            onChange={(_e, data) => setDarkMode(data.checked)}
            label={darkMode ? 'Dark' : 'Light'}
          />
          <Text size={200} style={{ color: 'var(--colorNeutralForeground3)', marginTop: '4px' }}>
            Theme switching will be available after desktop integration (Stage 7)
          </Text>
        </Field>

        <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
          <Button
            appearance="primary"
            icon={<SaveRegular />}
            onClick={handleSave}
          >
            Save Settings
          </Button>

          {saved && (
            <Text size={200} style={{ color: '#00b894' }}>
              Settings saved!
            </Text>
          )}
        </div>
      </Card>
    </div>
  );
};
