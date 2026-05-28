import { Text } from '@fluentui/react-components';
import { DeviceSession } from '../../types/session.ts';

interface Props {
  session: DeviceSession | null;
  visible: boolean;
}

export const DeviceInfoPanel = ({ session, visible }: Props) => {
  if (!visible || !session) return null;

  return (
    <div style={{
      display: 'flex',
      gap: '8px',
      flexWrap: 'wrap',
      padding: '8px 12px',
      backgroundColor: 'var(--colorNeutralBackground1)',
      borderRadius: '6px',
      border: '1px solid var(--colorNeutralStroke2)',
    }}>
      <div style={{
        padding: '4px 10px',
        borderRadius: '4px',
        backgroundColor: session.firmwareVersion ? '#00352a' : '#35120e',
        border: `1px solid ${session.firmwareVersion ? '#00b89444' : '#ff767544'}`,
      }}>
        <Text size={100} style={{ color: '#a0a0a0', marginRight: '6px' }}>FW</Text>
        <Text size={200} weight="semibold" style={{
          color: session.firmwareVersion ? '#00e6a8' : '#ff9b8a',
        }}>
          {session.firmwareVersion ? `V${session.firmwareVersion}` : 'None'}
        </Text>
      </div>

      {session.bootloaderRevision > 0 && (
        <div style={{
          padding: '4px 10px',
          borderRadius: '4px',
          backgroundColor: '#1a1a3a',
          border: '1px solid #6c5ce744',
        }}>
          <Text size={100} style={{ color: '#a0a0a0', marginRight: '6px' }}>BL</Text>
          <Text size={200} weight="semibold" style={{ color: '#a78bfa' }}>
            rev {session.bootloaderRevision}
          </Text>
        </div>
      )}

      {session.firmwareDescription && (
        <div style={{
          padding: '4px 10px',
          borderRadius: '4px',
          backgroundColor: '#1a1a2a',
          border: '1px solid #60a5fa44',
          maxWidth: '400px',
        }}>
          <Text size={200} style={{ color: '#93c5fd' }}>
            {session.firmwareDescription}
          </Text>
        </div>
      )}
    </div>
  );
};
