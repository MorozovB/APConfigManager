export interface DeviceProfile {
  id: string;
  name: string;
  description: string;
  boardType: number;
  parameterFilePath: string | null;
  firmwareFilePath: string | null;
  profileOptions: Record<string, boolean>;
  // profileOptions: { bootloader: false, firmware: false, parameters: false }
}
