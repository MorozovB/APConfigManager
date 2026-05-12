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
}


export interface CreateSessionRequest {
  port: string;
  baudRate: number;
}
