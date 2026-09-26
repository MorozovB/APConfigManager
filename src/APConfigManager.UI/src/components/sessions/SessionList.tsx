import { useState, useRef, useEffect, useCallback } from 'react';
import { Button, Text, Tooltip } from '@fluentui/react-components';
import { AddRegular, PlayFilled } from '@fluentui/react-icons';
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
                </div>
            )}

            {slots.map((id, position) => (
                <SessionSection
                    key={id}
                    slotId={id}
                    index={position}
                    total={slots.length || 1}
                    onClose={() => closeSlot(id)}
                    onRunningChange={handleRunningChange}
                    onConnectedChange={handleConnectedChange}
                    groupProfileId={groupProfileId}
                    groupRunToken={groupRunToken}
                />
            ))}

            {slots.length < MAX_SESSIONS && (
                <Button appearance="subtle" icon={<AddRegular />} onClick={addSlot}
                        style={{ alignSelf: 'flex-start' }}>
                    Add session
                </Button>
            )}
        </div>
    );
};