import { useState, useRef } from 'react';
import { Button } from '@fluentui/react-components';
import { AddRegular } from '@fluentui/react-icons';
import { SessionSection } from './SessionSection';

const MAX_SESSIONS = 4; // E2 сделает это значением из настроек (1–7)

export const SessionList = () => {
    const [slots, setSlots] = useState<number[]>([0]); // стартуем с одной сессии
    const nextId = useRef(1);

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
                    total={slots.length}
                    onClose={() => closeSlot(id)}
                />
            ))}

            {slots.length < MAX_SESSIONS && (
                <Button
                    appearance="subtle"
                    icon={<AddRegular />}
                    onClick={addSlot}
                    style={{ alignSelf: 'flex-start' }}
                >
                    Add session
                </Button>
            )}
        </div>
    );
};