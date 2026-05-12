export interface FlashResult {
  success: boolean;
  wasSameVersion: boolean;
  bytesWritten: number;
  firmwareVersion: string | null;
  errorMessage: string | null;
}

export interface EraseResult {
  success: boolean;
  errorMessage: string | null;
}

export interface ParamUploadResult {
  success: boolean;
  sent: number;
  total: number;
  failed: number;
  readOnly: number;
  hidden: number;
  errorMessage: string | null;
}

export interface OperationResult {
  success: boolean;
  message: string | null;
  data: unknown;
}

export interface Parameter {
  name: string;
  value: number;
}
