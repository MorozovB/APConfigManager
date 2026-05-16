import { Badge } from '@fluentui/react-components';
import { DeviceState } from '../../types/session';

const stateConfig: Record<DeviceState, { color: 'success' | 'warning' | 'danger' | 'informative' | 'important'; label: string }> = {
  Disconnected: { color: 'informative', label: 'Disconnected' },
  Connected: { color: 'success', label: 'Connected' },
  InBootloader: { color: 'warning', label: 'Bootloader' },
  Flashing: { color: 'important', label: 'Flashing' },
  Erasing: { color: 'danger', label: 'Erasing' },
  UploadingParams: { color: 'important', label: 'Uploading' },
};

interface Props {
  state: DeviceState;
}

export const DeviceStatusBadge = ({ state }: Props) => {
  const config = stateConfig[state];

  return (
    <Badge
      appearance="filled"
  color={config.color}
  style={{ minWidth: '90px', textAlign: 'center' }}
>
  {config.label}
  </Badge>
);
};
