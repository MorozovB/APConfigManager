export type DeviceState =
  | 'Disconnected'
  | 'Connected'
  | 'InBootloader'
  | 'Flashing'
  | 'Erasing'
  | 'UploadingParams';

export interface DeviceSession {
  id: string;
  port: string;
  baudRate: number;
  state: DeviceState;
  connectedAt: string;
  deviceSerial: string;
  firmwareVersion: string;
  firmwareDescription: string;
  bootloaderRevision: number;
}


export interface CreateSessionRequest {
  port: string;
  baudRate: number;
}
