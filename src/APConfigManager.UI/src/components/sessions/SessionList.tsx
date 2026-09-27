import { useState, useRef, useEffect, useCallback } from 'react';
import { Button, Text, Tooltip } from '@fluentui/react-components';
import { AddRegular, PlayFilled, PlugDisconnectedRegular } from '@fluentui/react-icons';
import { SessionSection } from './SessionSection';
import { ProfileSelector } from '../common/ProfileSelector';
import { useSettings } from '../../hooks/useSettings';
import { useProfiles } from '../../hooks/useProfiles';
import { useProfileFiles } from '../../hooks/useProfileFiles';
import { notifyOperationsFinished } from '../../platform/host';

const MAX_SESSIONS = 7;

export const SessionList = () => {
    const { settings } = useSettings();
    const { profiles } = useProfiles();
    const { loadFromServer } = useProfileFiles();

    const [slots, setSlots] = useState<number[]>([]);
    const nextId = useRef(0);
    const initialized = useRef(false);

    const runningRef = useRef<Map<number, boolean>>(new Map());
    const prevAnyRunning = useRef(false);
    const [anyRunning, setAnyRunning] = useState(false);

    const connectedRef = useRef<Map<number, boolean>>(new Map());
    const [connectedCount, setConnectedCount] = useState(0);

    // Group "run active": one profile applied to every connected session at once.
    const [groupProfileId, setGroupProfileId] = useState<string | null>(null);
    const [groupRunToken, setGroupRunToken] = useState(0);
    const [groupDisconnectToken, setGroupDisconnectToken] = useState(0);
    const [groupBusy, setGroupBusy] = useState(false);


    useEffect(() => {
        if (!initialized.current && settings) {
            initialized.current = true;
            const startup = Math.min(Math.max(settings.startupSessions ?? 1, 1), MAX_SESSIONS);
            setSlots(Array.from({ length: startup }, () => nextId.current++));
        }
    }, [settings]);

    const handleRunningChange = useCallback((id: number, running: boolean) => {
        runningRef.current.set(id, running);
        const anyRunningNow = Array.from(runningRef.current.values()).some(Boolean);
        setAnyRunning(anyRunningNow);

        if (prevAnyRunning.current && !anyRunningNow) {
            notifyOperationsFinished();
        }
        prevAnyRunning.current = anyRunningNow;
    }, []);

    const handleConnectedChange = useCallback((id: number, connected: boolean) => {
        connectedRef.current.set(id, connected);
        setConnectedCount(Array.from(connectedRef.current.values()).filter(Boolean).length);
    }, []);

    const handleRunActive = useCallback(async () => {
        if (!groupProfileId || connectedCount === 0) return;
        const profile = profiles.find(p => p.id === groupProfileId);
        if (!profile) return;

        // Shared file store: load the profile's files once; each connected session
        // reads them when it runs. Per-session run reports its own file errors.
        setGroupBusy(true);
        try {
            await loadFromServer(profile);
        } catch {
            /* ignore here — the sessions surface missing-file warnings */
        } finally {
            setGroupBusy(false);
        }
        setGroupRunToken(t => t + 1);
    }, [groupProfileId, connectedCount, profiles, loadFromServer]);

    const handleDisconnectAll = useCallback(() => {
        setGroupDisconnectToken(t => t + 1);
    }, []);

    const addSlot = () => {
        setSlots(prev => (prev.length >= MAX_SESSIONS ? prev : [...prev, nextId.current++]));
    };

    const closeSlot = (id: number) => {
        setSlots(prev => prev.filter(s => s !== id));
        runningRef.current.delete(id);
        connectedRef.current.delete(id);
        setConnectedCount(Array.from(connectedRef.current.values()).filter(Boolean).length);
    };

    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>

            {connectedCount > 0 && (
                <div style={{
                    display: 'flex', alignItems: 'center', gap: '12px', flexWrap: 'wrap',
                    padding: '10px 12px', borderRadius: '8px',
                    backgroundColor: 'var(--colorNeutralBackground2)',
                    border: '1px solid var(--colorBrandStroke1)',
                }}>
                    <Text size={200} weight="semibold">Run on connected</Text>
                    <ProfileSelector
                        profiles={profiles}
                        selectedProfileId={groupProfileId}
                        onSelect={setGroupProfileId}
                        disabled={anyRunning || groupBusy}
                    />
                    <Tooltip content="Run this profile on all connected sessions" relationship="label">
                        <Button
                            appearance="primary"
                            icon={<PlayFilled />}
                            onClick={handleRunActive}
                            disabled={!groupProfileId || connectedCount === 0 || anyRunning || groupBusy}
                            style={{ backgroundColor: '#0984e3', borderColor: '#0984e3' }}
                        >
                            Run active ({connectedCount})
                        </Button>
                    </Tooltip>
                    <Tooltip content="Disconnect all connected sessions" relationship="label">
                        <Button
                            appearance="subtle"
                            icon={<PlugDisconnectedRegular />}
                            onClick={handleDisconnectAll}
                            disabled={anyRunning || groupBusy}
                            style={{ color: '#d63031' }}
                        >
                            Disconnect all
                        </Button>
                    </Tooltip>
                </div>
            )}

            <div style={{ display: 'flex', flexWrap: 'wrap', gap: '16px', alignItems: 'stretch' }}>
                {slots.map((id, position) => (
                    <SessionSection
                        key={id}
                        slotId={id}
                        index={position}
                        onClose={() => closeSlot(id)}
                        onRunningChange={handleRunningChange}
                        onConnectedChange={handleConnectedChange}
                        groupProfileId={groupProfileId}
                        groupRunToken={groupRunToken}
                        groupDisconnectToken={groupDisconnectToken}
                    />
                ))}

                {slots.length < MAX_SESSIONS && (
                    <button
                        type="button"
                        onClick={addSlot}
                        title="Add session"
                        style={{
                            width: '460px',
                            minHeight: '280px',
                            display: 'flex',
                            flexDirection: 'column',
                            alignItems: 'center',
                            justifyContent: 'center',
                            gap: '10px',
                            cursor: 'pointer',
                            borderRadius: '10px',
                            border: '2px dashed var(--colorNeutralStroke2)',
                            background: 'var(--colorNeutralBackground2)',
                            color: 'var(--colorNeutralForeground3)',
                        }}
                    >
                        <AddRegular style={{ fontSize: '48px' }} />
                        <Text size={400} weight="semibold" style={{ color: 'var(--colorNeutralForeground2)' }}>
                            Add Section
                        </Text>
                    </button>
                )}
            </div>
        </div>
    );
};