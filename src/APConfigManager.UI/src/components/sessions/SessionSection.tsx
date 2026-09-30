import {useState, useCallback, useEffect, useRef, type CSSProperties} from 'react';
import {getParameter, setParameter} from '../../api/paramsApi';
import {addJournalEntry} from '../../api/journalApi';
import {
    Switch,
    Button,
    Text,
    Tooltip,
    makeStyles,
    switchClassNames,
} from '@fluentui/react-components';
import {
    PlayFilled,
    StopFilled,
    PlugConnectedRegular,
    PlugDisconnectedRegular,
    DismissRegular,
} from '@fluentui/react-icons';

import {usePorts} from '../../hooks/usePorts';
import {useDeviceSession} from '../../hooks/useDeviceSession';
import {useProfiles} from '../../hooks/useProfiles';
import {useSessionOrchestrator} from '../../hooks/useSessionOrchestrator';
import {useProfileFiles} from '../../hooks/useProfileFiles';
// import { useMockAccelerometer } from '../device/AccelerometerWidget';

import {PortSelector} from '../common/PortSelector';
import {ProfileSelector} from '../common/ProfileSelector';
import {CircularProgress} from '../common/CircularProgress';
import {LogEntry} from '../common/LogConsole';
import {DeviceStatusBadge} from '../device/DeviceStatusBadge';
import {useTranslation} from "react-i18next";
// import { AccelerometerWidget } from '../device/AccelerometerWidget';

const fieldBoxStyle: CSSProperties = {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: '8px',
    minHeight: '34px',
    boxSizing: 'border-box',
    padding: '4px 10px',
    borderRadius: '6px',
    border: '1px solid var(--colorNeutralStroke2)',
    backgroundColor: 'var(--colorNeutralBackground1)',
};

// Fluent reserves 8px of vertical margin around the Switch indicator, which made
// the ARMING chip grow taller than the sibling field chips once connected. Trim
// that margin so the switch fits the chip's fixed height.
const useStyles = makeStyles({
    armingSwitch: {
        [`& .${switchClassNames.indicator}`]: {
            marginTop: 0,
            marginBottom: 0,
        },
    },
});

interface Props {
    index: number;
    slotId: number;
    onClose: () => void;
    onRunningChange: (id: number, running: boolean) => void;
    onConnectedChange: (id: number, connected: boolean, port: string) => void;
    portsUsedByOthers: string[];
    groupProfileId: string | null;
    groupRunToken: number;
    groupDisconnectToken: number;
}

export const SessionSection = ({
                                   index,
                                   slotId,
                                   onClose,
                                   onRunningChange,
                                   onConnectedChange,
                                   portsUsedByOthers,
                                   groupProfileId,
                                   groupRunToken,
                                   groupDisconnectToken,
                               }: Props) => {
    // const [enabled, setEnabled] = useState(index === 0);
    const [selectedPort, setSelectedPort] = useState('');
    const [selectedProfileId, setSelectedProfileId] = useState<string | null>(null);
    const [blRevBefore, setBlRevBefore] = useState<number>(0);
    const [loadingProfileFiles, setLoadingProfileFiles] = useState(false);

    const {ports} = usePorts();
    const session = useDeviceSession();
    const {profiles} = useProfiles();
    const orchestrator = useSessionOrchestrator();
    const {getFiles, loadFromServer} = useProfileFiles();
    const [armingValue, setArmingValue] = useState<number | null>(null);
    const [armingBusy, setArmingBusy] = useState(false);
    const styles = useStyles();
    const {t} = useTranslation();
    const availablePorts = ports.filter(p => !portsUsedByOthers.includes(p.name));
    // const accelData = useMockAccelerometer();

    const addLog = useCallback((message: string, type: LogEntry['type'] = 'info') => {
        console.debug(`[session ${slotId}] ${type}: ${message}`);
    }, [slotId]);

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

    useEffect(() => {
        if (!session.sessionId || !session.data) {
            setArmingValue(null);
            return;
        }
        // Only read when the board is running firmware in normal mode.
        if (!session.data.firmwareVersion) {
            setArmingValue(null);
            return;
        }
        if (session.deviceState !== 'Connected') {
            setArmingValue(null);
            return;
        }
        let cancelled = false;
        setArmingValue(null); // show "reading…" while (re)fetching after a reboot
        (async () => {
            try {
                const p = await getParameter(session.sessionId!, 'ARMING_REQUIRE');
                if (!cancelled) setArmingValue(p.value);
            } catch {
                if (!cancelled) setArmingValue(null);
            }
        })();
        return () => {
            cancelled = true;
        };
    }, [session.sessionId, session.data?.firmwareVersion, session.deviceState, session.isConnected]);

    useEffect(() => {
        onRunningChange(slotId, orchestrator.isRunning);
    }, [slotId, orchestrator.isRunning, onRunningChange]);

    useEffect(() => {
        onConnectedChange(slotId, session.isConnected, session.data?.port ?? '');
    }, [slotId, session.isConnected, session.data?.port, onConnectedChange]);

    useEffect(() => {
        if (!session.isConnected && selectedPort && portsUsedByOthers.includes(selectedPort)) {
            setSelectedPort('');
        }
    }, [portsUsedByOthers, selectedPort, session.isConnected]);

    const handleConnect = useCallback(async () => {
        if (!selectedPort) {
            addLog('Select a port first', 'warn');
            return;
        }
        addLog(`Connecting to ${selectedPort}...`);
        await session.connect(selectedPort);
        if (session.error) {
            addLog(`Connection failed: ${session.error}`, 'error');
            void addJournalEntry({operation: 'Connect', port: selectedPort, success: false, message: session.error});
        } else {
            addLog(`Connected to ${selectedPort}`, 'success');
            void addJournalEntry({
                operation: 'Connect',
                port: selectedPort,
                deviceSerial: session.data?.deviceSerial ?? '',
                success: true,
                message: 'Connected',
            });
        }
    }, [selectedPort, session, addLog]);

    const handleDisconnect = useCallback(async () => {
        const port = session.data?.port ?? '';
        const deviceSerial = session.data?.deviceSerial ?? '';
        addLog('Disconnecting...');
        orchestrator.reset();
        await session.disconnect();
        addLog('Disconnected', 'info');
        void addJournalEntry({operation: 'Disconnect', port, deviceSerial, success: true, message: 'Disconnected'});
    }, [session, orchestrator, addLog]);

    const handleClose = useCallback(() => {
        if (session.isConnected) return;
        onClose();
    }, [session.isConnected, onClose]);

    const runProfileById = useCallback(async (profileId: string | null) => {
        if (!session.sessionId) {
            addLog('Not connected', 'warn');
            return;
        }
        const profile = profiles.find(p => p.id === profileId);
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
            session.resetProgress,
            {port: session.data?.port ?? '', deviceSerial: session.data?.deviceSerial ?? ''}
        );

        if (orchestrator.error) {
            addLog(`Process failed: ${orchestrator.error}`, 'error');
        } else {
            addLog('Process completed', 'success');
        }
    }, [session.sessionId, session.data, profiles, getFiles, orchestrator, addLog, session.refreshSession, session.resetProgress]);

    const handlePlay = useCallback(
        () => runProfileById(selectedProfileId),
        [runProfileById, selectedProfileId],
    );

    const handleStop = useCallback(() => {
        orchestrator.stop();
        addLog('Process stopped', 'warn');
    }, [orchestrator, addLog]);

    // When the container broadcasts a group run, connected sessions run the chosen
    // profile. A per-slot token ref prevents re-running on unrelated re-renders and
    // stops a freshly-added slot from replaying a past group run.
    const lastGroupRunToken = useRef(groupRunToken);
    useEffect(() => {
        if (groupRunToken === lastGroupRunToken.current) return;
        lastGroupRunToken.current = groupRunToken;
        if (session.isConnected && groupProfileId) {
            void runProfileById(groupProfileId);
        }
    }, [groupRunToken, groupProfileId, session.isConnected, runProfileById]);

    // Broadcast disconnect: connected sessions disconnect when the token changes.
    const lastGroupDisconnectToken = useRef(groupDisconnectToken);
    useEffect(() => {
        if (groupDisconnectToken === lastGroupDisconnectToken.current) return;
        lastGroupDisconnectToken.current = groupDisconnectToken;
        if (session.isConnected) {
            void handleDisconnect();
        }
    }, [groupDisconnectToken, session.isConnected, handleDisconnect]);

    const handleArmingToggle = useCallback(async () => {
        if (!session.sessionId || armingValue === null) return;
        const next = armingValue >= 0.5 ? 0 : 1;          // флип реального значения
        setArmingBusy(true);
        try {
            const result = await setParameter(session.sessionId, 'ARMING_REQUIRE', next);
            if (result.success) {
                setArmingValue(next);                          // подтверждено устройством
                addLog(`ARMING_REQUIRE → ${next}`, 'info');
            } else {
                addLog(result.message || 'Device did not confirm ARMING_REQUIRE', 'warn');
            }
        } catch (e) {
            addLog(e instanceof Error ? e.message : 'Failed to set ARMING_REQUIRE', 'warn');
        } finally {
            setArmingBusy(false);
        }
    }, [session.sessionId, armingValue, addLog]);

    const isBusy = orchestrator.isRunning || session.connecting || loadingProfileFiles;
    const showCompletedResults = (orchestrator.stage === 'done' || orchestrator.stage === 'error')
        && orchestrator.results.length > 0;
    const progressVisible = session.isConnected
        && orchestrator.stage !== 'done' && orchestrator.stage !== 'idle' && orchestrator.stage !== 'error';

    return (
        <div style={{
            width: '460px',
            flexShrink: 0,
            display: 'flex',
            flexDirection: 'column',
            backgroundColor: 'var(--colorNeutralBackground2)',
            borderRadius: '10px',
            border: `1px solid ${session.isConnected ? 'var(--colorBrandStroke1)' : 'var(--colorNeutralStroke1)'}`,
            overflow: 'hidden',
        }}>

            {/* Tab-style header: close + session number + mode badge */}
            <div style={{
                display: 'flex', alignItems: 'center', gap: '8px',
                padding: '6px 8px 6px 6px',
                backgroundColor: 'var(--colorNeutralBackground3)',
                borderBottom: '1px solid var(--colorNeutralStroke1)',
            }}>
                <Button
                    appearance="subtle"
                    size="small"
                    icon={<DismissRegular/>}
                    onClick={handleClose}
                    disabled={isBusy || session.isConnected}
                    title={session.isConnected ? t('sessions.disconnectBeforeClosing') : t('sessions.closeSession')}
                    style={{color: session.isConnected ? undefined : '#d63031', minWidth: 'auto'}}
                />
                <Text size={300} weight="semibold">{t('sessions.session')} {index + 1}</Text>
                <div style={{flex: 1}}/>
                <DeviceStatusBadge state={session.deviceState}/>
            </div>

            {/* Body */}
            <div style={{display: 'flex', flexDirection: 'column', gap: '12px', padding: '12px'}}>

                {/* Two-column control grid */}
                <div style={{display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px', alignItems: 'center'}}>

                    {/* Port | Connect / Disconnect */}
                    <PortSelector
                        ports={availablePorts}
                        selectedPort={selectedPort}
                        onSelect={setSelectedPort}
                        disabled={session.isConnected || isBusy}
                    />
                    {session.isConnected ? (
                        <Button appearance="subtle" icon={<PlugDisconnectedRegular/>}
                                onClick={handleDisconnect} disabled={isBusy}
                                style={{color: '#d63031', width: '100%'}}>
                            {t('common.disconnect')}
                        </Button>
                    ) : (
                        <Button appearance="primary" icon={<PlugConnectedRegular/>}
                                onClick={handleConnect} disabled={!selectedPort || isBusy}
                                style={{width: '100%'}}>
                            {t('common.connect')}
                        </Button>
                    )}

                    {/* Profile | Start + Stop */}
                    <ProfileSelector profiles={profiles} selectedProfileId={selectedProfileId}
                                     onSelect={setSelectedProfileId} disabled={isBusy}/>
                    <div style={{display: 'flex', gap: '8px'}}>
                        <Tooltip content={t('sessions.startProcess')} relationship="label">
                            <Button appearance="primary" icon={<PlayFilled/>} onClick={handlePlay}
                                    disabled={!session.isConnected || !selectedProfileId || isBusy || loadingProfileFiles}
                                    style={{
                                        backgroundColor: '#00b894',
                                        borderColor: '#00b894',
                                        flex: 1,
                                        minWidth: '40px'
                                    }}/>
                        </Tooltip>
                        <Tooltip content={t('sessions.stopProcess')} relationship="label">
                            <Button appearance="subtle" icon={<StopFilled/>} onClick={handleStop}
                                    disabled={!orchestrator.isRunning}
                                    style={{color: '#d63031', flex: 1, minWidth: '40px'}}/>
                        </Tooltip>
                    </div>

                    {/* Arming | Altitude */}
                    <div style={fieldBoxStyle}>
                        <Text size={100} style={{color: 'var(--colorNeutralForeground3)'}}>ARMING_REQUIRE</Text>
                        {!session.isConnected ? (
                            <Text size={300} weight="semibold">—</Text>
                        ) : armingValue === null ? (
                            <Text size={200} style={{color: 'var(--colorNeutralForeground3)'}}>{t('sessions.reading')}</Text>
                        ) : (
                            <div style={{display: 'flex', alignItems: 'center', gap: '6px'}}>
                                <Text size={200} weight={armingValue < 0.5 ? 'bold' : 'regular'}>0</Text>
                                <Switch className={styles.armingSwitch} checked={armingValue >= 0.5}
                                        disabled={armingBusy} onChange={handleArmingToggle}/>
                                <Text size={200} weight={armingValue >= 0.5 ? 'bold' : 'regular'}>1</Text>
                            </div>
                        )}
                    </div>
                    <TileField
                        label={t('sessions.altitude')}
                        value={session.isConnected && session.altitude !== null ? `${session.altitude.toFixed(1)} m` : '—'}
                    />

                    {/* Version FW | BL */}
                    <TileField
                        label={t('sessions.versionFw')}
                        value={session.data?.firmwareVersion ? `V${session.data.firmwareVersion}` : '—'}
                    />
                    <TileField
                        label={t('sessions.bl')}
                        value={session.data && session.data.bootloaderRevision > 0 ? `rev ${session.data.bootloaderRevision}` : '—'}
                    />
                </div>

                {/* Circular progress + completed results */}
                {(progressVisible || showCompletedResults) && (
                    <div style={{display: 'flex', alignItems: 'center', gap: '16px', flexWrap: 'wrap'}}>
                        {progressVisible && (
                            <div style={{display: 'flex', flexDirection: 'column', alignItems: 'center', gap: '4px'}}>
                                <CircularProgress percent={session.progress.percent} size={40} stroke={5}/>
                                <Text size={100} style={{color: 'var(--colorNeutralForeground3)'}}>
                                    {session.progress.message || orchestrator.stage}
                                </Text>
                            </div>
                        )}

                        {showCompletedResults && (
                            <div style={{display: 'flex', flexDirection: 'column', gap: '4px'}}>
                                {orchestrator.results
                                    .filter(r => r.stage !== 'done')
                                    .map((r, i) => {
                                        if (r.stage === 'flashing')
                                            return <Text key={i} size={200} weight="semibold"
                                                         style={{color: r.success ? '#00b894' : '#ff7675'}}>
                                                {`${t('sessions.firmware')} — ${r.success ? t('sessions.done') : t('sessions.failed')} ${r.success ? '✓' : '✗'}`}
                                            </Text>;
                                        if (r.stage === 'bootloader') {
                                            if (r.success) {
                                                const blAfter = session.data?.bootloaderRevision || 0;
                                                const revInfo = blRevBefore > 0 && blAfter > 0 ? ` (rev ${blRevBefore} → ${blAfter})` : '';
                                                return <Text key={i} size={200} weight="semibold"
                                                             style={{color: '#00b894'}}>
                                                    {`${t('sessions.bootloader')} — ${t('sessions.done')} ✓`}{revInfo}</Text>;
                                            }
                                            return <Text key={i} size={200} weight="semibold"
                                                         style={{color: '#ff7675'}}>
                                                {`${t('sessions.bootloader')} — ${t('sessions.failed')} ✗`}</Text>;
                                        }
                                        if (r.stage === 'params')
                                            return <Text key={i} size={200} weight="semibold"
                                                         style={{color: r.success ? '#00b894' : '#ff7675'}}>
                                                {`${t('sessions.parameters')} — ${r.success ? t('sessions.done') : t('sessions.failed')} ${r.success ? '✓' : '✗'}`}
                                            </Text>;
                                        return null;
                                    })}
                            </div>
                        )}
                    </div>
                )}

                {session.error && (
                    <div style={{
                        padding: '8px 12px', borderRadius: '4px',
                        backgroundColor: session.error.includes('disconnected') ? '#35120e' : undefined,
                        border: session.error.includes('disconnected') ? '1px solid #ff767544' : undefined,
                    }}>
                        <Text size={200} style={{color: '#ff7675'}}>{session.error}</Text>
                    </div>
                )}
            </div>
        </div>
    );
};


interface TileFieldProps {
    label: string;
    value: string;
}

/** Thin-bordered label + value chip for the session tile grid: label left, value right. */
const TileField = ({label, value}: TileFieldProps) => (
    <div style={fieldBoxStyle}>
        <Text size={100} style={{color: 'var(--colorNeutralForeground3)'}}>{label}</Text>
        <Text size={300} weight="semibold">{value}</Text>
    </div>
);
