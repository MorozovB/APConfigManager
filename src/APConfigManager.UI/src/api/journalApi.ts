import apiClient from './apiClient';
import { JournalEntry, CreateJournalEntry } from '../types/journal';

export const getJournal = async (limit = 200): Promise<JournalEntry[]> => {
    const response = await apiClient.get<JournalEntry[]>('/journal', { params: { limit } });
    return response.data;
};

/**
 * Appends an entry to the operation journal. Best-effort: journaling must never
 * break the operation it records, so failures are swallowed.
 */
export const addJournalEntry = async (entry: CreateJournalEntry): Promise<void> => {
    try {
        await apiClient.post('/journal', entry);
    } catch {
        /* ignore — the operation itself already reported its own result */
    }
};

export const clearJournal = async (): Promise<void> => {
    await apiClient.delete('/journal');
};