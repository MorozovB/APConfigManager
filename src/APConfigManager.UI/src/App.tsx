import { FluentProvider } from '@fluentui/react-components';
import { useState, useMemo, useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { darkTheme, lightTheme } from './styles/theme';
import { ThemeModeContext, type ThemeMode } from './contexts/ThemeModeContext';
import { useSettings } from './hooks/useSettings';
import { TitleBar } from './components/layout/TitleBar';
import { SessionList } from './components/sessions/SessionList';
import { ProfilesPage } from './components/profiles/ProfilesPage';
import { ToolsPage } from './components/tools/ToolsPage';
import { SettingsPage } from './components/settings/SettingsPage';
import { JournalPage } from './components/journal/JournalPage';
import { useActiveSessions } from './hooks/useActiveSessions';
import { NavButton } from './components/layout/NavButton';

type TabId = 'config' | 'profiles' | 'tools' | 'settings'| 'journal';

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

    const selectTab = (tab: TabId) => {
        if (tab !== activeTab && hasActiveSessions) return;
        setActiveTab(tab);
    };

    const isTabLocked = (tab: TabId) => hasActiveSessions && activeTab !== tab;

    return (
        <ThemeModeContext.Provider value={themeCtx}>
            <FluentProvider theme={themeMode === 'dark' ? darkTheme : lightTheme} style={{ height: '100vh', display: 'flex', flexDirection: 'column' }}>
                <TitleBar />

                <div style={{ padding: '8px 16px', display: 'flex', alignItems: 'center', gap: '12px' }}>
                    <NavButton label={t('tabs.config')} active={activeTab === 'config'}
                               disabled={isTabLocked('config')} onClick={() => selectTab('config')} />
                    <NavButton label={t('tabs.profiles')} active={activeTab === 'profiles'}
                               disabled={isTabLocked('profiles')} onClick={() => selectTab('profiles')} />
                    <NavButton label={t('tabs.tools')} active={activeTab === 'tools'}
                               disabled={isTabLocked('tools')} onClick={() => selectTab('tools')} />
                    <NavButton label={t('tabs.journal')} active={activeTab === 'journal'}
                               disabled={isTabLocked('journal')} onClick={() => selectTab('journal')} />
                    <NavButton label={t('tabs.settings')} active={activeTab === 'settings'}
                               disabled={isTabLocked('settings')} onClick={() => selectTab('settings')} />
                </div>

                <div style={{ flex: 1, padding: '16px', overflow: 'auto' }}>
                    {activeTab === 'config' && <SessionList />}
                    {activeTab === 'profiles' && <ProfilesPage />}
                    {activeTab === 'tools' && <ToolsPage />}
                    {activeTab === 'settings' && <SettingsPage />}
                    {activeTab === 'journal' && <JournalPage />}
                </div>
            </FluentProvider>
        </ThemeModeContext.Provider>
    );
}

export default App;