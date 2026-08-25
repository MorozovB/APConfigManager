import { useState, useEffect, useCallback } from 'react';
import { PortInfo } from '../types/device';
import { getPorts } from '../api/portsApi';

export const usePorts = () => {
    const [ports, setPorts] = useState<PortInfo[]>([]);

    const [loading, setLoading] = useState(false);

    const [error, setError] = useState<string | null>(null);

    const refresh = useCallback(async () => {
        setLoading(true);
        setError(null);
        try {
            const data = await getPorts();
            setPorts(data);
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Failed to load ports');
        } finally {
            setLoading(false);
        }
    }, []);

    useEffect(() => {
        // Первоначальная загрузка
        refresh();

        const interval = setInterval(refresh, 3000);

        return () => clearInterval(interval);
    }, [refresh]);

    return { ports, loading, error, refresh };
};
