import { useState, useEffect, useCallback } from 'react';
import { JournalEntry } from '../types/journal';
import { getJournal, clearJournal } from '../api/journalApi';

export const useJournal = () => {
    const [entries, setEntries] = useState<JournalEntry[]>([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const load = useCallback(async () => {
        setLoading(true);
        setError(null);
        try {
            setEntries(await getJournal());
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Failed to load journal');
        } finally {
            setLoading(false);
        }
    }, []);

    const clear = useCallback(async () => {
        await clearJournal();
        await load();
    }, [load]);

    useEffect(() => {
        load();
    }, [load]);

    return { entries, loading, error, reload: load, clear };
};