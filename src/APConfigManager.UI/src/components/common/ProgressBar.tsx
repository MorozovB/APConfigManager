import {ProgressBar as FluentProgressBar, Text} from '@fluentui/react-components';
import {useTranslation} from "react-i18next";

interface Props {
    percent: number;
    message: string;
    visible?: boolean;
}

export const ProgressBar = ({percent, message, visible = true}: Props) => {
    const {t} = useTranslation();
    if (!visible) return null;

    return (
        <div style={{
            padding: '8px 0',
            display: 'flex',
            flexDirection: 'column',
            gap: '4px'
        }}>
            <div style={{
                display: 'flex',
                justifyContent: 'space-between',
                alignItems: 'center'
            }}>
                <Text size={200} style={{color: 'var(--colorNeutralForeground3)'}}>
                    {message || t('common.processInfo')}
                </Text>
                <Text size={200} weight="semibold" style={{color: 'var(--colorBrandForeground1)'}}>
                    {percent}%
                </Text>
            </div>

            <FluentProgressBar
                value={percent / 100}
                thickness="large"
                color="brand"
            />
        </div>
    );
};
