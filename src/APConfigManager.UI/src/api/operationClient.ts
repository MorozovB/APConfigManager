import apiClient from './apiClient';
import { OperationResult } from '../types/operations';

/**
 * POSTs a long-running device operation (flash / params upload / bootloader).
 *
 * In Electron it is routed through the main process (Node sockets, no per-host
 * connection cap), so a 7-session group run isn't throttled by Chromium's limit
 * of 6 concurrent connections per host. When the bridge isn't available (e.g. the
 * SPA opened in a plain browser during development) it falls back to axios.
 *
 * `path` is the API path relative to `/api` (e.g. `/sessions/<id>/flash`).
 * Progress and completion still arrive over SignalR, exactly as before.
 */
export const postOperation = async (path: string, file?: File): Promise<OperationResult> => {
    const bridge = window.electronAPI?.apiOperation;

    if (bridge) {
        const fileBuffer = file ? await file.arrayBuffer() : undefined;
        const res = await bridge({ path: `/api${path}`, fileName: file?.name, fileBuffer });
        const data = res.body ? JSON.parse(res.body) : {};

        if (res.status < 200 || res.status >= 300) {
            throw new Error((data && data.message) || `Request failed (${res.status})`);
        }
        return data as OperationResult;
    }

    // Browser fallback — subject to Chromium's 6-connections-per-host limit.
    const formData = new FormData();
    if (file) formData.append('file', file);

    const response = await apiClient.post<OperationResult>(
        path,
        file ? formData : null,
        {
            headers: file ? { 'Content-Type': 'multipart/form-data' } : undefined,
            timeout: 600000,
        },
    );
    return response.data;
};