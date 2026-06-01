import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr';

export type ProgressCallback = (percent: number, message: string) => void;

export type ParamProgressCallback = (current: number, total: number) => void;

export type StateChangedCallback = (sessionId: string, state: string) => void;

export type OperationCompletedCallback = (sessionId: string, result: unknown) => void;

export type AltitudeCallback = (altitude: number) => void;


export const createHubConnection = (): HubConnection => {
  const connection = new HubConnectionBuilder()
    .withUrl('/hubs/device')


    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])

    .configureLogging(LogLevel.Information)

    .build();

  return connection;
};

export const subscribeToSession = async (
  connection: HubConnection,
  sessionId: string
): Promise<void> => {
  await connection.invoke('SubscribeToSession', sessionId);
};

export const unsubscribeFromSession = async (
  connection: HubConnection,
  sessionId: string
): Promise<void> => {
  await connection.invoke('UnsubscribeFromSession', sessionId);
};

export const registerHandlers = (
  connection: HubConnection,
  handlers: {
    onFlashProgress?: ProgressCallback;
    onEraseProgress?: ProgressCallback;
    onParamProgress?: ParamProgressCallback;
    onStateChanged?: StateChangedCallback;
    onOperationCompleted?: OperationCompletedCallback;
    onAltitudeUpdate?: AltitudeCallback;
  }
): void => {

  if (handlers.onFlashProgress) {
    connection.on('FlashProgress', (percent: number, message: string) => {
      handlers.onFlashProgress!(percent, message);
    });
  }

  if (handlers.onEraseProgress) {
    connection.on('EraseProgress', (percent: number, message: string) => {
      handlers.onEraseProgress!(percent, message);
    });
  }

  if (handlers.onParamProgress) {
    connection.on('ParamProgress', (current: number, total: number) => {
      handlers.onParamProgress!(current, total);
    });
  }

  if (handlers.onStateChanged) {
    connection.on('DeviceStateChanged', (sessionId: string, state: string) => {
      handlers.onStateChanged!(sessionId, state);
    });
  }

  if (handlers.onOperationCompleted) {
    connection.on('OperationCompleted', (sessionId: string, result: unknown) => {
      handlers.onOperationCompleted!(sessionId, result);
    });
  }

  if (handlers.onAltitudeUpdate) {
    connection.on('AltitudeUpdate', (altitude: number) => {
      handlers.onAltitudeUpdate!(altitude);
    });
  }
};

export const removeHandlers = (connection: HubConnection): void => {
  connection.off('FlashProgress');
  connection.off('EraseProgress');
  connection.off('ParamProgress');
  connection.off('DeviceStateChanged');
  connection.off('OperationCompleted');
  connection.off('AltitudeUpdate');
};

export const startConnection = async (connection: HubConnection): Promise<void> => {
  try {
    await connection.start();
    console.log('SignalR connected');
  } catch (error) {
    console.error('SignalR connection failed:', error);
    // Повторная попытка через 5 секунд
    setTimeout(() => startConnection(connection), 5000);
  }
};

export const stopConnection = async (connection: HubConnection): Promise<void> => {
  try {
    await connection.stop();
    console.log('SignalR disconnected');
  } catch (error) {
    console.error('SignalR disconnect failed:', error);
  }
};
