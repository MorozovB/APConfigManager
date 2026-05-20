import { Text } from '@fluentui/react-components';

interface Props {
  altitude: number | null;
}

export const AltitudeDisplay = ({ altitude }: Props) => {
  return (
    <div style={{
      backgroundColor: 'var(--colorNeutralBackground3)',
      border: '1px solid var(--colorNeutralStroke1)',
      borderRadius: '6px',
      padding: '12px',
      minWidth: '120px',
      textAlign: 'center',
      display: 'flex',
      flexDirection: 'column',
      gap: '4px',
    }}>
      <Text size={200} style={{ color: 'var(--colorNeutralForeground3)' }}>
        Altitude
      </Text>
      <Text size={600} weight="bold" style={{ color: 'var(--colorBrandForeground1)' }}>
        {altitude !== null ? `${altitude.toFixed(1)} m` : '—'}
      </Text>
    </div>
  );
};
