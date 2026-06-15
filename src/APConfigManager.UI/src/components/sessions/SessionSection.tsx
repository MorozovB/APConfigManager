import { useState, useCallback, useEffect } from 'react';
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
import { useProfileFiles } from '../../hooks/useProfileFiles';
// import { useMockAccelerometer } from '../device/AccelerometerWidget';

import { PortSelector } from '../common/PortSelector';
import { ProfileSelector } from '../common/ProfileSelector';
import { ProgressBar } from '../common/ProgressBar';
import { LogConsole, LogEntry } from '../common/LogConsole';
import { DeviceStatusBadge } from '../device/DeviceStatusBadge';
import { DeviceInfoPanel } from '../device/DeviceInfoPanel';
import { AltitudeDisplay } from '../device/AltitudeDisplay';
// import { AccelerometerWidget } from '../device/AccelerometerWidget';

interface Props {
  index: number;
}

export const SessionSection = ({ index }: Props) => {
  const [enabled, setEnabled] = useState(index === 0);
  const [selectedPort, setSelectedPort] = useState('');
  const [selectedProfileId, setSelectedProfileId] = useState<string | null>(null);
  const [showLogs, setShowLogs] = useState(false);
  const [logEntries, setLogEntries] = useState<LogEntry[]>([]);
  const [blRevBefore, setBlRevBefore] = useState<number>(0);
  const [loadingProfileFiles, setLoadingProfileFiles] = useState(false);

  const { ports } = usePorts();
  const session = useDeviceSession();
  const { profiles } = useProfiles();
  const orchestrator = useSessionOrchestrator();
  const { getFiles, loadFromServer } = useProfileFiles();
  // const accelData = useMockAccelerometer();

  const addLog = useCallback((message: string, type: LogEntry['type'] = 'info') => {
    const timestamp = new Date().toLocaleTimeString();
    setLogEntries(prev => [...prev, { timestamp, message, type }]);
  }, []);

  useEffect(() => {
    if (!selectedProfileId) return;
    const profile = profiles.find(p => p.id === selectedProfileId);
    if (!profile) return;

    let cancelled = false;

    const load = async () => {
      setLoadingProfileFiles(true);
      try {
        await loadFromServer(profile);
        if (!cancelled) {
          addLog(`Profile files loaded: "${profile.name}"`, 'success');
        }
      } catch (err) {
        if (!cancelled) {
          const message = err instanceof Error ? err.message : 'Failed to load profile files';
          addLog(message, 'error');
        }
      } finally {
        if (!cancelled) {
          setLoadingProfileFiles(false);
        }
      }
    };

    load();

    return () => {
      cancelled = true;
    };
  }, [selectedProfileId, profiles, loadFromServer, addLog]);

  useEffect(() => {
    const livePort = session.data?.port;
    if (livePort) {
      setSelectedPort(prev => (prev === livePort ? prev : livePort));
    }
  }, [session.data?.port]);

  useEffect(() => {
    if (session.logEntries.length === 0) return;
    const latest = session.logEntries[session.logEntries.length - 1];
    addLog(latest, 'progress');
  }, [session.logEntries.length, session.logEntries, addLog]);

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
    orchestrator.reset();
    await session.disconnect();
    addLog('Disconnected', 'info');
  }, [session, orchestrator, addLog]);

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

    const files = getFiles(profile.id);

    if (profile.profileOptions?.firmware && !files.firmwareFile) {
      addLog('No firmware file in profile. Edit profile and select .apj file.', 'warn');
      return;
    }
    if (profile.profileOptions?.parameters && !files.paramFile) {
      addLog('No parameter file in profile. Edit profile and select .param file.', 'warn');
      return;
    }

    setBlRevBefore(session.data?.bootloaderRevision || 0);

    addLog(`Starting process with profile "${profile.name}"...`, 'info');
    await orchestrator.start(
      session.sessionId,
      profile,
      files.firmwareFile,
      files.paramFile,
      session.refreshSession,
      session.resetProgress
    );

    if (orchestrator.error) {
      addLog(`Process failed: ${orchestrator.error}`, 'error');
    } else {
      addLog('Process completed', 'success');
    }
  }, [session.sessionId, session.data, selectedProfileId, profiles, getFiles, orchestrator, addLog, session.refreshSession, session.resetProgress]);

  const handleStop = useCallback(() => {
    orchestrator.stop();
    addLog('Process stopped', 'warn');
  }, [orchestrator, addLog]);

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
          disabled={session.isConnected}
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

  const isBusy = orchestrator.isRunning || session.connecting || loadingProfileFiles;
  const showCompletedResults = (orchestrator.stage === 'done' || orchestrator.stage === 'error')
    && orchestrator.results.length > 0;

  return (
    <div style={{
      padding: '16px',
      backgroundColor: 'var(--colorNeutralBackground2)',
      borderRadius: '8px',
      border: `1px solid ${session.isConnected ? 'var(--colorBrandStroke1)' : 'var(--colorNeutralStroke1)'}`,
      display: 'flex',
      gap: '12px',
      maxHeight: 'calc((100vh - 120px) / 4)',
      overflow: 'hidden',
    }}>

      <div style={{ display: 'flex', flexDirection: 'column', gap: '12px', flexShrink: 0 }}>

        <div style={{ display: 'flex', alignItems: 'center', gap: '12px', flexWrap: 'wrap' }}>
          <Switch
            checked={enabled}
            onChange={(_e, data) => {
              if (session.isConnected) return;
              setEnabled(data.checked);
            }}
            disabled={session.isConnected || isBusy}
          />
          <PortSelector
            ports={ports}
            selectedPort={selectedPort}
            onSelect={setSelectedPort}
            disabled={session.isConnected || isBusy}
          />
          <Button appearance="primary" icon={<PlugConnectedRegular />}
                  onClick={handleConnect}
                  disabled={session.isConnected || !selectedPort || isBusy}>
            Connect
          </Button>
          <Button appearance="subtle" icon={<PlugDisconnectedRegular />}
                  onClick={handleDisconnect}
                  disabled={!session.isConnected || isBusy}
                  style={{ color: session.isConnected ? '#d63031' : undefined }}>
            Disconnect
          </Button>
          <DeviceStatusBadge state={session.deviceState} />
          <DeviceInfoPanel session={session.data} visible={session.isConnected} />
          <Button appearance="subtle" icon={<TextBulletListLtrRegular />}
                  onClick={() => setShowLogs(!showLogs)} size="small">
            {showLogs ? 'Hide' : 'Logs'}
          </Button>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: '12px', flexWrap: 'wrap' }}>
          <ProfileSelector profiles={profiles} selectedProfileId={selectedProfileId}
                           onSelect={setSelectedProfileId} disabled={isBusy} />
          <Tooltip content="Start process" relationship="label">
            <Button appearance="primary" icon={<PlayFilled />} onClick={handlePlay}
                    disabled={!session.isConnected || !selectedProfileId || isBusy || loadingProfileFiles}
                    style={{ backgroundColor: '#00b894', borderColor: '#00b894', minWidth: '40px' }} />
          </Tooltip>
          <Tooltip content="Stop process" relationship="label">
            <Button appearance="subtle" icon={<StopFilled />} onClick={handleStop}
                    disabled={!orchestrator.isRunning}
                    style={{ color: '#d63031', minWidth: '40px' }} />
          </Tooltip>

          {session.isConnected && (
            <div style={{
              display: 'flex', gap: '12px', padding: '3px 8px',
              backgroundColor: 'var(--colorNeutralBackground1)',
              borderRadius: '6px', border: '1px solid var(--colorNeutralStroke2)',
              alignItems: 'center',
            }}>
              <AltitudeDisplay altitude={session.altitude} />
            </div>
          )}

          {showCompletedResults && orchestrator.results
            .filter(r => r.stage !== 'done')
            .map((r, i) => {
              if (r.stage === 'flashing')
                return <Text key={i} size={200} weight="semibold"
                             style={{ color: r.success ? '#00b894' : '#ff7675' }}>
                  {r.success ? 'Firmware — Done ✓' : 'Firmware — Failed ✗'}
                </Text>;
              if (r.stage === 'bootloader') {
                if (r.success) {
                  const blAfter = session.data?.bootloaderRevision || 0;
                  const revInfo = blRevBefore > 0 && blAfter > 0 ? ` (rev ${blRevBefore} → ${blAfter})` : '';
                  return <Text key={i} size={200} weight="semibold" style={{ color: '#00b894' }}>
                    Bootloader — Done ✓{revInfo}</Text>;
                }
                return <Text key={i} size={200} weight="semibold" style={{ color: '#ff7675' }}>
                  Bootloader — Failed ✗</Text>;
              }
              if (r.stage === 'params')
                return <Text key={i} size={200} weight="semibold"
                             style={{ color: r.success ? '#00b894' : '#ff7675' }}>
                  {r.success ? 'Parameters — Done ✓' : 'Parameters — Failed ✗'}
                </Text>;
              return null;
            })}
        </div>

        <ProgressBar
          percent={session.progress.percent}
          message={session.progress.message || orchestrator.stage}
          visible={session.isConnected && orchestrator.stage !== 'done' && orchestrator.stage !== 'idle' && orchestrator.stage !== 'error'}
        />

        {session.error && (
          <div style={{
            padding: '8px 12px', borderRadius: '4px',
            backgroundColor: session.error.includes('disconnected') ? '#35120e' : undefined,
            border: session.error.includes('disconnected') ? '1px solid #ff767544' : undefined,
          }}>
            <Text size={200} style={{ color: '#ff7675' }}>{session.error}</Text>
          </div>
        )}
      </div>

      {showLogs && (
        <div style={{ flex: 1, minWidth: '250px', minHeight: 0, overflow: 'hidden' }}>
          <LogConsole entries={logEntries} visible={true} />
        </div>
      )}
    </div>
  );
};
