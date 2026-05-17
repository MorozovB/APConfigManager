import { FluentProvider, TabList, Tab, SelectTabEvent, SelectTabData } from '@fluentui/react-components';
import { useState } from 'react';
import { darkTheme } from './styles/theme';
import { AppHeader } from './components/layout/AppHeader';
import { SessionList } from './components/sessions/SessionList';
import { ProfilesPage } from './components/profiles/ProfilesPage';
import { ToolsPage } from './components/tools/ToolsPage';
import { SettingsPage } from './components/settings/SettingsPage';

type TabId = 'config' | 'profiles' | 'tools' | 'settings';

function App() {
  const [activeTab, setActiveTab] = useState<TabId>('config');

  const handleTabSelect = (_event: SelectTabEvent, data: SelectTabData) => {
    setActiveTab(data.value as TabId);
  };

  return (
    <FluentProvider theme={darkTheme} style={{ height: '100vh', display: 'flex', flexDirection: 'column' }}>
      <AppHeader />

      <div style={{ padding: '0 16px' }}>
        <TabList selectedValue={activeTab} onTabSelect={handleTabSelect} size="large">
          <Tab value="config">Config</Tab>
          <Tab value="profiles">Profiles</Tab>
          <Tab value="tools">Tools</Tab>
          <Tab value="settings">Settings</Tab>
        </TabList>
      </div>

      <div style={{ flex: 1, padding: '16px', overflow: 'auto' }}>
        {activeTab === 'config' && <SessionList />}
        {activeTab === 'profiles' && <ProfilesPage />}
        {activeTab === 'tools' && <ToolsPage />}
        {activeTab === 'settings' && <SettingsPage />}
      </div>
    </FluentProvider>
  );
}

export default App;
