import { useState, useRef, useEffect } from 'react';
import { Button } from '@fluentui/react-components';
import { AddRegular } from '@fluentui/react-icons';
import { SessionSection } from './SessionSection';
import { useSettings } from '../../hooks/useSettings';

const MAX_SESSIONS = 7;

export const SessionList = () => {
    const { settings } = useSettings();

    const [slots, setSlots] = useState<number[]>([]);
    const nextId = useRef(0);
    const initialized = useRef(false);

    useEffect(() => {
        if (!initialized.current && settings) {
            initialized.current = true;
            const startup = Math.min(Math.max(settings.startupSessions ?? 1, 1), MAX_SESSIONS);
            setSlots(Array.from({ length: startup }, () => nextId.current++));
        }
    }, [settings]);

    const addSlot = () => {
        setSlots(prev => (prev.length >= MAX_SESSIONS ? prev : [...prev, nextId.current++]));
    };

    const closeSlot = (id: number) => {
        setSlots(prev => prev.filter(s => s !== id));
    };

    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
            {slots.map((id, position) => (
                <SessionSection
                    key={id}
                    index={position}
                    total={slots.length || 1}
                    onClose={() => closeSlot(id)}
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