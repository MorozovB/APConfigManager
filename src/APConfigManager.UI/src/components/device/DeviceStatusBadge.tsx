import {Badge} from '@fluentui/react-components';
import {DeviceState} from '../../types/session';
import {useTranslation} from "react-i18next";

const stateConfig: Record<DeviceState, { color: 'success' | 'warning' | 'danger' | 'informative' | 'important'; labelKey: string }> = {
    Disconnected: { color: 'informative', labelKey: 'device.disconnected' },
    Connected: { color: 'success', labelKey: 'device.connected' },
    InBootloader: { color: 'warning', labelKey: 'device.bootloader' },
    Flashing: { color: 'important', labelKey: 'device.flashing' },
    Erasing: { color: 'danger', labelKey: 'device.erasing' },
    UploadingParams: { color: 'important', labelKey: 'device.uploading' },
};

interface Props {
    state: DeviceState;
}

export const DeviceStatusBadge = ({state}: Props) => {
    const {t} = useTranslation();
    const config = stateConfig[state];

    return (
        <Badge
            appearance="filled"
            color={config.color}
            style={{minWidth: '90px', textAlign: 'center'}}
        >
            {t(config.labelKey)}
        </Badge>
    );
};