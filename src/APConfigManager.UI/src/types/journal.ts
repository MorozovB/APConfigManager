export interface JournalEntry {
    id: string;
    timestampUtc: string;
    operation: string;
    port: string;
    deviceSerial: string;
    profileName: string;
    success: boolean;
    message: string;
}

export interface CreateJournalEntry {
    operation: string;
    port?: string;
    deviceSerial?: string;
    profileName?: string;
    success: boolean;
    message?: string;
}