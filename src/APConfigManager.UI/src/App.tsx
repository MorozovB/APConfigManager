import { FluentProvider, TabList, Tab, SelectTabEvent, SelectTabData, Text } from '@fluentui/react-components';
import { useState } from 'react';
import { darkTheme } from './styles/theme';
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

  const handleTabSelect = (_event: SelectTabEvent, data: SelectTabData) => {
    const tab = data.value as TabId;
    if (tab !== 'config' && hasActiveSessions) return;
    setActiveTab(tab);
  };

  return (
    <FluentProvider theme={darkTheme} style={{ height: '100vh', display: 'flex', flexDirection: 'column' }}>
      <AppHeader />

      <div style={{ padding: '0 16px', display: 'flex', alignItems: 'center', gap: '12px' }}>
        <TabList selectedValue={activeTab} onTabSelect={handleTabSelect} size="large">
          <Tab value="config">Config</Tab>
          <Tab
            value="profiles"
            disabled={hasActiveSessions}
            style={hasActiveSessions ? { opacity: 0.4, cursor: 'not-allowed' } : undefined}
          >
            Profiles
          </Tab>
          <Tab
            value="tools"
            disabled={hasActiveSessions}
            style={hasActiveSessions ? { opacity: 0.4, cursor: 'not-allowed' } : undefined}
          >
            Tools
          </Tab>
          <Tab
            value="settings"
            disabled={hasActiveSessions}
            style={hasActiveSessions ? { opacity: 0.4, cursor: 'not-allowed' } : undefined}
          >
            Settings
          </Tab>
        </TabList>

        {hasActiveSessions}
      </div>

      <div style={{ flex: 1, padding: '16px', overflow: 'auto' }}>
        {activeTab === 'config' && <SessionList />}
        {activeTab === 'profiles' && !hasActiveSessions && <ProfilesPage />}
        {activeTab === 'tools' && !hasActiveSessions && <ToolsPage />}
        {activeTab === 'settings' && !hasActiveSessions && <SettingsPage />}
      </div>
    </FluentProvider>
  );
}

export default App;
