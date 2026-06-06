import { useState, useEffect } from 'react';
import { getSessions } from '../api/sessionsApi';

export const useActiveSessions = () => {
  const [hasActive, setHasActive] = useState(false);

  useEffect(() => {
    let cancelled = false;

    const check = async () => {
      try {
        const sessions = await getSessions();
        if (!cancelled) {
          setHasActive(sessions.length > 0);
        }
      } catch {
        if (!cancelled) {
          setHasActive(false);
        }
      }
    };

    check();
    const interval = setInterval(check);

    return () => {
      cancelled = true;
      clearInterval(interval);
    };
  }, []);

  return hasActive;
};
