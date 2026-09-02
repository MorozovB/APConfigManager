import { useState, useRef, useEffect, useCallback } from 'react';
import { Button } from '@fluentui/react-components';
import { AddRegular } from '@fluentui/react-icons';
import { SessionSection } from './SessionSection';
import { useSettings } from '../../hooks/useSettings';
import { notifyOperationsFinished } from '../../platform/host';

const MAX_SESSIONS = 7;

export const SessionList = () => {
    const { settings } = useSettings();

    const [slots, setSlots] = useState<number[]>([]);
    const nextId = useRef(0);
    const initialized = useRef(false);

    const runningRef = useRef<Map<number, boolean>>(new Map());
    const prevAnyRunning = useRef(false);

    useEffect(() => {
        if (!initialized.current && settings) {
            initialized.current = true;
            const startup = Math.min(Math.max(settings.startupSessions ?? 1, 1), MAX_SESSIONS);
            setSlots(Array.from({ length: startup }, () => nextId.current++));
        }
    }, [settings]);

    const handleRunningChange = useCallback((id: number, running: boolean) => {
        runningRef.current.set(id, running);
        const anyRunning = Array.from(runningRef.current.values()).some(Boolean);

        if (prevAnyRunning.current && !anyRunning) {
            notifyOperationsFinished();
        }

        prevAnyRunning.current = anyRunning;
    }, []);

    const addSlot = () => {
        setSlots(prev => (prev.length >= MAX_SESSIONS ? prev : [...prev, nextId.current++]));
    };

    const closeSlot = (id: number) => {
        setSlots(prev => prev.filter(s => s !== id));
        runningRef.current.delete(id);   // убрать состояние закрытого слота
    };

    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
            {slots.map((id, position) => (
                <SessionSection
                    key={id}
                    slotId={id}
                    index={position}
                    total={slots.length || 1}
                    onClose={() => closeSlot(id)}
                    onRunningChange={handleRunningChange}
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