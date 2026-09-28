import {useState, useCallback} from 'react';
import {Text, Button} from '@fluentui/react-components';
import {
    EraserRegular,
    ArrowResetRegular,
} from '@fluentui/react-icons';
import {useTranslation} from 'react-i18next';

import {usePorts} from '../../hooks/usePorts';
import {useDeviceSession} from '../../hooks/useDeviceSession';
import {useEraseOperation} from '../../hooks/useEraseOperation';
import {ToolCard} from './ToolCard';
import {PortSelector} from '../common/PortSelector';
import {ConfirmDialog} from '../common/ConfirmDialog';
import {ProgressBar} from '../common/ProgressBar';

type ConfirmAction = 'erase' | 'resetParams' | null;

export const ToolsPage = () => {
    const {t} = useTranslation();
    const {ports} = usePorts();
    const session = useDeviceSession();
    const eraseOp = useEraseOperation();

    const [selectedPort, setSelectedPort] = useState('');
    const [confirmAction, setConfirmAction] = useState<ConfirmAction>(null);

    const [eraseStatus, setEraseStatus] = useState<{
        message: string;
        type: 'success' | 'error' | 'info'
    } | null>(null);
    const [resetStatus, setResetStatus] = useState<{
        message: string;
        type: 'success' | 'error' | 'info'
    } | null>(null);

    const [resetLoading, setResetLoading] = useState(false);

    const handleConnect = useCallback(async () => {
        if (!selectedPort) return;
        setEraseStatus(null);
        setResetStatus(null);
        await session.connect(selectedPort);
    }, [selectedPort, session]);

    const handleDisconnect = useCallback(async () => {
        await session.disconnect();
        setEraseStatus(null);
        setResetStatus(null);
    }, [session]);

    const handleEraseClick = useCallback(() => {
        setConfirmAction('erase');
    }, []);

    const handleEraseConfirm = useCallback(async () => {
        setConfirmAction(null);
        if (!session.sessionId) return;

        setEraseStatus({message: t('tools.erasing'), type: 'info'});
        const result = await eraseOp.execute(session.sessionId);

        if (result?.success) {
            setEraseStatus({message: t('tools.eraseSuccess'), type: 'success'});
        } else {
            setEraseStatus({
                message: eraseOp.error || result?.message || t('tools.eraseFailed'),
                type: 'error',
            });
        }
    }, [session.sessionId, eraseOp, t]);

    const handleResetClick = useCallback(() => {
        setConfirmAction('resetParams');
    }, []);

    const handleResetConfirm = useCallback(async () => {
        setConfirmAction(null);
        if (!session.sessionId) return;

        setResetLoading(true);
        setResetStatus({message: t('tools.resetting'), type: 'info'});

        try {
            const {resetParams} = await import('../../api/paramsApi');
            const result = await resetParams(session.sessionId);
            if (result.success) {
                setResetStatus({message: t('tools.resetSuccess'), type: 'success'});
            } else {
                setResetStatus({message: result.message || t('tools.resetFailed'), type: 'error'});
            }
        } catch (err) {
            setResetStatus({
                message: err instanceof Error ? err.message : t('tools.resetFailed'),
                type: 'error',
            });
        } finally {
            setResetLoading(false);
        }
    }, [session.sessionId, t]);


    const handleConfirmCancel = useCallback(() => {
        setConfirmAction(null);
    }, []);

    const confirmDialogs: Record<string, { title: string; message: string; confirmText: string }> = {
        erase: {
            title: t('tools.eraseFirmware'),
            message: t('tools.eraseConfirmMessage'),
            confirmText: t('tools.erase'),
        },
        resetParams: {
            title: t('tools.resetParameters'),
            message: t('tools.resetConfirmMessage'),
            confirmText: t('tools.reset'),
        },
    };

    const needsConnection = !session.isConnected;
    const isBusy = eraseOp.isRunning || resetLoading;

    return (
        <div style={{display: 'flex', flexDirection: 'column', gap: '16px'}}>

            <Text size={500} weight="semibold">{t('tools.title')}</Text>

            <div style={{
                display: 'flex',
                alignItems: 'center',
                gap: '12px',
                padding: '12px 16px',
                backgroundColor: 'var(--colorNeutralBackground2)',
                borderRadius: '8px',
                border: '1px solid var(--colorNeutralStroke1)',
                flexWrap: 'wrap',
            }}>
                <PortSelector
                    ports={ports}
                    selectedPort={selectedPort}
                    onSelect={setSelectedPort}
                    disabled={session.isConnected || isBusy}
                />

                {!session.isConnected ? (
                    <Button
                        appearance="primary"
                        onClick={handleConnect}
                        disabled={!selectedPort || isBusy}
                    >
                        {t('common.connect')}
                    </Button>
                ) : (
                    <Button
                        appearance="subtle"
                        onClick={handleDisconnect}
                        disabled={isBusy}
                        style={{color: '#d63031'}}
                    >
                        {t('common.disconnect')}
                    </Button>
                )}

                {session.isConnected && (
                    <Text size={200} style={{color: '#00b894'}}>
                        {t('tools.connectedTo', { port: session.data?.port })}
                    </Text>
                )}

                {session.error && (
                    <Text size={200} style={{color: '#ff7675'}}>
                        {session.error}
                    </Text>
                )}
            </div>

            <ProgressBar
                percent={session.progress.percent}
                message={session.progress.message}
                visible={isBusy}
            />

            <div style={{
                display: 'grid',
                gridTemplateColumns: 'repeat(auto-fill, minmax(350px, 1fr))',
                gap: '16px',
            }}>

                <ToolCard
                    icon={<EraserRegular/>}
                    title={t('tools.eraseFirmware')}
                    description={t('tools.eraseFirmwareDesc')}
                    buttonText={t('tools.erase')}
                    buttonColor="#d63031"
                    onClick={handleEraseClick}
                    disabled={needsConnection || isBusy}
                    loading={eraseOp.isRunning}
                    statusMessage={eraseStatus?.message}
                    statusType={eraseStatus?.type}
                />

                <ToolCard
                    icon={<ArrowResetRegular/>}
                    title={t('tools.resetParameters')}
                    description={t('tools.resetParametersDesc')}
                    buttonText={t('tools.resetToDefaults')}
                    buttonColor="#e17055"
                    onClick={handleResetClick}
                    disabled={needsConnection || isBusy}
                    loading={resetLoading}
                    statusMessage={resetStatus?.message}
                    statusType={resetStatus?.type}
                />

            </div>

            {confirmAction && (
                <ConfirmDialog
                    open={true}
                    title={confirmDialogs[confirmAction].title}
                    message={confirmDialogs[confirmAction].message}
                    confirmText={confirmDialogs[confirmAction].confirmText}
                    onConfirm={
                        confirmAction === 'erase' ? handleEraseConfirm : handleResetConfirm
                    }
                    onCancel={handleConfirmCancel}
                />
            )}
        </div>
    );
};
