export interface DeviceProfile {
  id: string;
  name: string;
  description: string;
  boardType: number;
  parameterFilePath: string | null;
  firmwareFilePath: string | null;
  parameterFileName: string | null;
  firmwareFileName: string | null;
  profileOptions: Record<string, boolean>;
  // profileOptions: { bootloader: false, firmware: false, parameters: false }
}
