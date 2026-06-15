import { useState, useCallback } from 'react';
import { Text, Button  } from '@fluentui/react-components';
import {
  EraserRegular,
  ArrowResetRegular,
} from '@fluentui/react-icons';

import { usePorts } from '../../hooks/usePorts';
import { useDeviceSession } from '../../hooks/useDeviceSession';
import { useEraseOperation } from '../../hooks/useEraseOperation';
import { ToolCard } from './ToolCard';
import { PortSelector } from '../common/PortSelector';
import { ConfirmDialog } from '../common/ConfirmDialog';
import { ProgressBar } from '../common/ProgressBar';

type ConfirmAction = 'erase' | 'resetParams' | null;

export const ToolsPage = () => {
  const { ports } = usePorts();
  const session = useDeviceSession();
  const eraseOp = useEraseOperation();

  const [selectedPort, setSelectedPort] = useState('');
  const [confirmAction, setConfirmAction] = useState<ConfirmAction>(null);

  const [eraseStatus, setEraseStatus] = useState<{ message: string; type: 'success' | 'error' | 'info' } | null>(null);
  const [resetStatus, setResetStatus] = useState<{ message: string; type: 'success' | 'error' | 'info' } | null>(null);

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

    setEraseStatus({ message: 'Erasing...', type: 'info' });
    const result = await eraseOp.execute(session.sessionId);

    if (result?.success) {
      setEraseStatus({ message: 'Firmware erased successfully', type: 'success' });
    } else {
      setEraseStatus({
        message: eraseOp.error || result?.message || 'Erase failed',
        type: 'error',
      });
    }
  }, [session.sessionId, eraseOp]);

  const handleResetClick = useCallback(() => {
    setConfirmAction('resetParams');
  }, []);

  const handleResetConfirm = useCallback(async () => {
    setConfirmAction(null);
    if (!session.sessionId) return;

    setResetLoading(true);
    setResetStatus({ message: 'Resetting parameters...', type: 'info' });

    try {
      const { resetParams } = await import('../../api/paramsApi');
      const result = await resetParams(session.sessionId);
      if (result.success) {
        setResetStatus({ message: 'Parameters reset to defaults', type: 'success' });
      } else {
        setResetStatus({ message: result.message || 'Reset failed', type: 'error' });
      }
    } catch (err) {
      setResetStatus({
        message: err instanceof Error ? err.message : 'Reset failed',
        type: 'error',
      });
    } finally {
      setResetLoading(false);
    }
  }, [session.sessionId]);


  const handleConfirmCancel = useCallback(() => {
    setConfirmAction(null);
  }, []);

  const confirmDialogs: Record<string, { title: string; message: string; confirmText: string }> = {
    erase: {
      title: 'Erase Firmware',
      message: 'This will completely erase the firmware from the device. The device will not boot until new firmware is flashed. Continue?',
      confirmText: 'Erase',
    },
    resetParams: {
      title: 'Reset Parameters',
      message: 'This will reset ALL parameters to factory defaults. Custom settings will be lost. The device will reboot. Continue?',
      confirmText: 'Reset',
    },
  };

  const needsConnection = !session.isConnected;
  const isBusy = eraseOp.isRunning || resetLoading;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>

      <Text size={500} weight="semibold">Tools</Text>

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
            Connect
          </Button>
        ) : (
          <Button
            appearance="subtle"
            onClick={handleDisconnect}
            disabled={isBusy}
            style={{ color: '#d63031' }}
          >
            Disconnect
          </Button>
        )}

        {session.isConnected && (
          <Text size={200} style={{ color: '#00b894' }}>
            Connected to {session.data?.port}
          </Text>
        )}

        {session.error && (
          <Text size={200} style={{ color: '#ff7675' }}>
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
          icon={<EraserRegular />}
          title="Erase Firmware"
          description="Completely erase the firmware from the device flash memory."
          buttonText="Erase"
          buttonColor="#d63031"
          onClick={handleEraseClick}
          disabled={needsConnection || isBusy}
          loading={eraseOp.isRunning}
          statusMessage={eraseStatus?.message}
          statusType={eraseStatus?.type}
        />

        <ToolCard
          icon={<ArrowResetRegular />}
          title="Reset Parameters"
          description="Reset all autopilot parameters to factory defaults."
          buttonText="Reset to Defaults"
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
