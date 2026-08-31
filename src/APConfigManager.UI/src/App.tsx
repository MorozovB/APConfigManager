import { FluentProvider, TabList, Tab, SelectTabEvent, SelectTabData } from '@fluentui/react-components';
import { useState, useMemo, useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { darkTheme, lightTheme } from './styles/theme';
import { ThemeModeContext, type ThemeMode } from './contexts/ThemeModeContext';
import { useSettings } from './hooks/useSettings';
import { AppHeader } from './components/layout/AppHeader';
import { SessionList } from './components/sessions/SessionList';
import { ProfilesPage } from './components/profiles/ProfilesPage';
import { ToolsPage } from './components/tools/ToolsPage';
import { SettingsPage } from './components/settings/SettingsPage';
import { useActiveSessions } from './hooks/useActiveSessions';

type TabId = 'config' | 'profiles' | 'tools' | 'settings';

function App() {
    const [activeTab, setActiveTab] = useState<TabId>('config');
    const hasActiveSessions = useActiveSessions();
    const { settings } = useSettings();
    const { t, i18n } = useTranslation();

    // тема
    const [themeOverride, setThemeOverride] = useState<ThemeMode | null>(null);
    const themeMode: ThemeMode = themeOverride ?? (settings?.theme === 'light' ? 'light' : 'dark');
    const themeCtx = useMemo(
        () => ({ mode: themeMode, setMode: (m: ThemeMode) => setThemeOverride(m) }),
        [themeMode],
    );

    const langInitialized = useRef(false);

    useEffect(() => {
        if (!langInitialized.current && settings?.language) {
            langInitialized.current = true;
            localStorage.setItem('lang', settings.language);
            void i18n.changeLanguage(settings.language);
        }
    }, [settings?.language, i18n]);

    const [, bump] = useState(0);

    useEffect(() => {
        const onChanged = () => bump((x) => x + 1);
        i18n.on('languageChanged', onChanged);
        return () => { i18n.off('languageChanged', onChanged); };
    }, [i18n]);

    const handleTabSelect = (_event: SelectTabEvent, data: SelectTabData) => {
        const tab = data.value as TabId;
        if (tab !== activeTab && hasActiveSessions) return;
        setActiveTab(tab);
    };

    const isTabLocked = (tab: TabId) => hasActiveSessions && activeTab !== tab && tab !== 'config';

    return (
        <ThemeModeContext.Provider value={themeCtx}>
            <FluentProvider theme={themeMode === 'dark' ? darkTheme : lightTheme} style={{ height: '100vh', display: 'flex', flexDirection: 'column' }}>
                <AppHeader />

                <div style={{ padding: '0 16px', display: 'flex', alignItems: 'center', gap: '12px' }}>
                    <TabList selectedValue={activeTab} onTabSelect={handleTabSelect} size="large">
                        <Tab value="config" disabled={isTabLocked('config')}
                             style={isTabLocked('config') ? { opacity: 0.4, cursor: 'not-allowed' } : undefined}>
                            {t('tabs.config')}
                        </Tab>
                        <Tab value="profiles" disabled={isTabLocked('profiles')}
                             style={isTabLocked('profiles') ? { opacity: 0.4, cursor: 'not-allowed' } : undefined}>
                            {t('tabs.profiles')}
                        </Tab>
                        <Tab value="tools" disabled={isTabLocked('tools')}
                             style={isTabLocked('tools') ? { opacity: 0.4, cursor: 'not-allowed' } : undefined}>
                            {t('tabs.tools')}
                        </Tab>
                        <Tab value="settings" disabled={isTabLocked('settings')}
                             style={isTabLocked('settings') ? { opacity: 0.4, cursor: 'not-allowed' } : undefined}>
                            {t('tabs.settings')}
                        </Tab>
                    </TabList>
                </div>

                <div style={{ flex: 1, padding: '16px', overflow: 'auto' }}>
                    {activeTab === 'config' && <SessionList />}
                    {activeTab === 'profiles' && <ProfilesPage />}
                    {activeTab === 'tools' && <ToolsPage />}
                    {activeTab === 'settings' && <SettingsPage />}
                </div>
            </FluentProvider>
        </ThemeModeContext.Provider>
    );
}

export default App;