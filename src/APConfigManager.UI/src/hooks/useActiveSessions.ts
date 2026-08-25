import { useState, useEffect, useRef } from 'react';
import { getSessions } from '../api/sessionsApi';

export const useActiveSessions = () => {
    const [hasActive, setHasActive] = useState(false);
    const falseCount = useRef(0);

    useEffect(() => {
        let cancelled = false;

        const check = async () => {
            try {
                const sessions = await getSessions();
                if (cancelled) return;

                if (sessions.length > 0) {
                    falseCount.current = 0;
                    setHasActive(true);
                } else {
                    falseCount.current++;
                    if (falseCount.current >= 3) {
                        setHasActive(false);
                    }
                }
            } catch {
                // API error during operation — keep current state, don't flicker
            }
        };

        check();
        const interval = setInterval(check, 500);

        return () => {
            cancelled = true;
            clearInterval(interval);
        };
    }, []);

    return hasActive;
};
