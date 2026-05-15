import { useState, useCallback, useEffect, useRef } from 'react';
import { DeviceSession, DeviceState } from '../types/session';
import { createSession, closeSession } from '../api/sessionsApi';
import {
  createHubConnection,
  startConnection,
  stopConnection,
  subscribeToSession,
  unsubscribeFromSession,
  registerHandlers,
  removeHandlers,
} from '../signalr/deviceHubConnection';
import { HubConnection } from '@microsoft/signalr';

export interface ProgressState {
  percent: number;
  message: string;
}

export const useDeviceSession = () => {

  const [data, setData] = useState<DeviceSession | null>(null);

  const [deviceState, setDeviceState] = useState<DeviceState>('Disconnected');

  const [progress, setProgress] = useState<ProgressState>({ percent: 0, message: '' });

  const [lastResult, setLastResult] = useState<unknown>(null);

  const [connecting, setConnecting] = useState(false);

  const [error, setError] = useState<string | null>(null);

  const connectionRef = useRef<HubConnection | null>(null);


  useEffect(() => {
    const connection = createHubConnection();
    connectionRef.current = connection;

    registerHandlers(connection, {
      onFlashProgress: (percent, message) => {
        setProgress({ percent, message });
      },
      onEraseProgress: (percent, message) => {
        setProgress({ percent, message });
      },
      onParamProgress: (current, total) => {
        const percent = total > 0 ? Math.round((100 * current) / total) : 0;
        setProgress({ percent, message: `${current}/${total}` });
      },
      onStateChanged: (_sessionId, state) => {
        setDeviceState(state as DeviceState);
      },
      onOperationCompleted: (_sessionId, result) => {
        setLastResult(result);
      },
    });

    connection.onreconnected(() => {
      console.log('SignalR reconnected');
      if (data?.id) {
        subscribeToSession(connection, data.id);
      }
    });

    startConnection(connection);

    return () => {
      removeHandlers(connection);
      stopConnection(connection);
    };
  }, []);

  const connect = useCallback(async (port: string, baudRate: number = 115200) => {
    setConnecting(true);
    setError(null);
    setProgress({ percent: 0, message: '' });
    setLastResult(null);

    try {
      const session = await createSession({ port, baudRate });
      setData(session);
      setDeviceState(session.state);

      if (connectionRef.current) {
        await subscribeToSession(connectionRef.current, session.id);
      }
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Connection failed';
      setError(message);
    } finally {
      setConnecting(false);
    }
  }, []);

  const disconnect = useCallback(async () => {
    if (!data) return;

    try {
      if (connectionRef.current) {
        await unsubscribeFromSession(connectionRef.current, data.id);
      }
      await closeSession(data.id);
    } catch (err) {
      console.error('Disconnect error:', err);
    } finally {
      setData(null);
      setDeviceState('Disconnected');
      setProgress({ percent: 0, message: '' });
      setLastResult(null);
      setError(null);
    }
  }, [data]);

  const resetProgress = useCallback(() => {
    setProgress({ percent: 0, message: '' });
    setLastResult(null);
  }, []);

  return {
    data,
    deviceState,
    progress,
    lastResult,
    connecting,
    error,
    isConnected: data !== null,
    sessionId: data?.id ?? null,

    connect,
    disconnect,
    resetProgress,
  };
};
