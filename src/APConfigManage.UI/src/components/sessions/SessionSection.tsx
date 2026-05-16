import { useState, useCallback } from 'react';
import {
  Switch,
  Button,
  Text,
  Tooltip,
} from '@fluentui/react-components';
import {
  PlayFilled,
  StopFilled,
  PlugConnectedRegular,
  PlugDisconnectedRegular,
  TextBulletListLtrRegular,
} from '@fluentui/react-icons';

import { usePorts } from '../../hooks/usePorts';
import { useDeviceSession } from '../../hooks/useDeviceSession';
import { useProfiles } from '../../hooks/useProfiles';
import { useSessionOrchestrator } from '../../hooks/useSessionOrchestrator';
import { useMockAccelerometer } from '../device/AccelerometerWidget';

import { PortSelector } from '../common/PortSelector';
import { ProfileSelector } from '../common/ProfileSelector';
import { ProgressBar } from '../common/ProgressBar';
import { LogConsole, LogEntry } from '../common/LogConsole';
import { DeviceStatusBadge } from '../device/DeviceStatusBadge';
import { AltitudeDisplay } from '../device/AltitudeDisplay';
import { AccelerometerWidget } from '../device/AccelerometerWidget';

interface Props {
  index: number;
}

export const SessionSection = ({ index }: Props) => {

  const [enabled, setEnabled] = useState(index === 0);

  const [selectedPort, setSelectedPort] = useState('');

  const [selectedProfileId, setSelectedProfileId] = useState<string | null>(null);

  const [showLogs, setShowLogs] = useState(false);

  const [logEntries, setLogEntries] = useState<LogEntry[]>([]);

  const [firmwareFile, setFirmwareFile] = useState<File | null>(null);
  const [paramFile, setParamFile] = useState<File | null>(null);

  const { ports } = usePorts();
  const session = useDeviceSession();
  const { profiles } = useProfiles();
  const orchestrator = useSessionOrchestrator();
  const accelData = useMockAccelerometer();

  const addLog = useCallback((message: string, type: LogEntry['type'] = 'info') => {
    const timestamp = new Date().toLocaleTimeString();
    setLogEntries(prev => [...prev, { timestamp, message, type }]);
  }, []);

  const handleConnect = useCallback(async () => {
    if (!selectedPort) {
      addLog('Select a port first', 'warn');
      return;
    }
    addLog(`Connecting to ${selectedPort}...`);
    await session.connect(selectedPort);
    if (session.error) {
      addLog(`Connection failed: ${session.error}`, 'error');
    } else {
      addLog(`Connected to ${selectedPort}`, 'success');
    }
  }, [selectedPort, session, addLog]);

  const handleDisconnect = useCallback(async () => {
    addLog('Disconnecting...');
    await session.disconnect();
    addLog('Disconnected', 'info');
  }, [session, addLog]);

  const handlePlay = useCallback(async () => {
    if (!session.sessionId) {
      addLog('Not connected', 'warn');
      return;
    }
    const profile = profiles.find(p => p.id === selectedProfileId);
    if (!profile) {
      addLog('Select a profile first', 'warn');
      return;
    }
    addLog(`Starting process with profile "${profile.name}"...`, 'info');
    await orchestrator.start(session.sessionId, profile, firmwareFile, paramFile);
    if (orchestrator.error) {
      addLog(`Process failed: ${orchestrator.error}`, 'error');
    } else {
      addLog('Process completed', 'success');
    }
  }, [session.sessionId, selectedProfileId, profiles, firmwareFile, paramFile, orchestrator, addLog]);

  const handleStop = useCallback(() => {
    orchestrator.stop();
    addLog('Process stopped', 'warn');
  }, [orchestrator, addLog]);

  const handleFirmwareFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0] || null;
    setFirmwareFile(file);
    if (file) addLog(`Firmware file: ${file.name}`, 'info');
  };

  const handleParamFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0] || null;
    setParamFile(file);
    if (file) addLog(`Param file: ${file.name}`, 'info');
  };

  if (!enabled) {
    return (
      <div style={{
        display: 'flex',
        alignItems: 'center',
        gap: '12px',
        padding: '12px 16px',
        backgroundColor: 'var(--colorNeutralBackground2)',
        borderRadius: '8px',
        border: '1px solid var(--colorNeutralStroke1)',
      }}>
        <Switch
          checked={enabled}
          onChange={(_e, data) => setEnabled(data.checked)}
        />
        <PortSelector
          ports={ports}
          selectedPort={selectedPort}
          onSelect={setSelectedPort}
          disabled={false}
        />
        <Text size={200} style={{ color: 'var(--colorNeutralForeground3)' }}>
          Session {index + 1}
        </Text>
      </div>
    );
  }

  const isBusy = orchestrator.isRunning || session.connecting;

  return (
    <div style={{
      padding: '16px',
      backgroundColor: 'var(--colorNeutralBackground2)',
      borderRadius: '8px',
      border: `1px solid ${session.isConnected ? 'var(--colorBrandStroke1)' : 'var(--colorNeutralStroke1)'}`,
      display: 'flex',
      flexDirection: 'column',
      gap: '12px',
    }}>

      <div style={{ display: 'flex', alignItems: 'center', gap: '12px', flexWrap: 'wrap' }}>
        <Switch
          checked={enabled}
          onChange={(_e, data) => {
            if (session.isConnected) {
              session.disconnect();
            }
            setEnabled(data.checked);
          }}
        />

        <PortSelector
          ports={ports}
          selectedPort={selectedPort}
          onSelect={setSelectedPort}
          disabled={session.isConnected || isBusy}
        />

        <Button
          appearance="primary"
          icon={<PlugConnectedRegular />}
          onClick={handleConnect}
          disabled={session.isConnected || !selectedPort || isBusy}
        >
          Connect
        </Button>

        <Button
          appearance="subtle"
          icon={<PlugDisconnectedRegular />}
          onClick={handleDisconnect}
          disabled={!session.isConnected || isBusy}
          style={{ color: session.isConnected ? '#d63031' : undefined }}
        >
          Disconnect
        </Button>

        <DeviceStatusBadge state={session.deviceState} />
      </div>

      <div style={{ display: 'flex', alignItems: 'center', gap: '12px', flexWrap: 'wrap' }}>
        <ProfileSelector
          profiles={profiles}
          selectedProfileId={selectedProfileId}
          onSelect={setSelectedProfileId}
          disabled={isBusy}
        />

        <Tooltip content="Start process" relationship="label">
          <Button
            appearance="primary"
            icon={<PlayFilled />}
            onClick={handlePlay}
            disabled={!session.isConnected || !selectedProfileId || isBusy}
            style={{ backgroundColor: '#00b894', borderColor: '#00b894', minWidth: '40px' }}
          />
        </Tooltip>

        <Tooltip content="Stop process" relationship="label">
          <Button
            appearance="subtle"
            icon={<StopFilled />}
            onClick={handleStop}
            disabled={!orchestrator.isRunning}
            style={{ color: '#d63031', minWidth: '40px' }}
          />
        </Tooltip>

        {/*<label style={{*/}
        {/*  display: 'flex',*/}
        {/*  alignItems: 'center',*/}
        {/*  gap: '4px',*/}
        {/*  cursor: 'pointer',*/}
        {/*  fontSize: '12px',*/}
        {/*  color: 'var(--colorNeutralForeground3)',*/}
        {/*}}>*/}
        {/*  <input*/}
        {/*    type="file"*/}
        {/*    accept=".apj"*/}
        {/*    onChange={handleFirmwareFileChange}*/}
        {/*    style={{ display: 'none' }}*/}
        {/*  />*/}
        {/*  <Button as="span" size="small" appearance="outline">*/}
        {/*    {firmwareFile ? firmwareFile.name : 'Firmware (.apj)'}*/}
        {/*  </Button>*/}
        {/*</label>*/}

        <label style={{ cursor: 'pointer' }}>
          <input
            type="file"
            accept=".apj"
            onChange={handleFirmwareFileChange}
            style={{ display: 'none' }}
          />
          <Button
            size="small"
            appearance="outline"
            onClick={(e) => {
              // Клик на кнопке → кликаем на скрытый input через label
              const input = (e.currentTarget as HTMLElement).parentElement?.querySelector('input');
              input?.click();
            }}
          >
            {firmwareFile ? firmwareFile.name : 'Firmware (.apj)'}
          </Button>
        </label>

        {/*<label style={{*/}
        {/*  display: 'flex',*/}
        {/*  alignItems: 'center',*/}
        {/*  gap: '4px',*/}
        {/*  cursor: 'pointer',*/}
        {/*  fontSize: '12px',*/}
        {/*  color: 'var(--colorNeutralForeground3)',*/}
        {/*}}>*/}
        {/*  <input*/}
        {/*    type="file"*/}
        {/*    accept=".param"*/}
        {/*    onChange={handleParamFileChange}*/}
        {/*    style={{ display: 'none' }}*/}
        {/*  />*/}
        {/*  <Button as="span" size="small" appearance="outline">*/}
        {/*    {paramFile ? paramFile.name : 'Params (.param)'}*/}
        {/*  </Button>*/}
        {/*</label>*/}

        <label style={{ cursor: 'pointer' }}>
          <input
            type="file"
            accept=".param"
            onChange={handleParamFileChange}
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
            {paramFile ? paramFile.name : 'Params (.param)'}
          </Button>
        </label>
      </div>

      <div style={{ display: 'flex', gap: '12px', alignItems: 'flex-start' }}>
        <div style={{ flex: 1 }}>
          <ProgressBar
            percent={session.progress.percent}
            message={session.progress.message || orchestrator.stage}
            visible={session.isConnected}
          />
        </div>

        <AltitudeDisplay altitude={session.isConnected ? 0 : null} />

        <AccelerometerWidget data={session.isConnected ? accelData : []} />
      </div>

      <div>
        <Button
          appearance="subtle"
          icon={<TextBulletListLtrRegular />}
          onClick={() => setShowLogs(!showLogs)}
          size="small"
        >
          {showLogs ? 'Hide logs' : 'Show logs'}
        </Button>
        <LogConsole entries={logEntries} visible={showLogs} />
      </div>
      {(session.error || orchestrator.error) && (
        <Text size={200} style={{ color: '#ff7675' }}>
          {session.error || orchestrator.error}
        </Text>
      )}
    </div>
  );
};
