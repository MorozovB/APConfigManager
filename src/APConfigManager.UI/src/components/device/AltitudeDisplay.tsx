import { useEffect, useRef, useState } from 'react';
import { Text } from '@fluentui/react-components';

interface Props {
  altitude: number | null;
}

export const AltitudeDisplay = ({ altitude }: Props) => {
  const ref = useRef<HTMLDivElement | null>(null);
  const [compact, setCompact] = useState(false);

  useEffect(() => {
    const el = ref.current;
    if (!el) return;

    const observer = new ResizeObserver(() => {
      const h = el.clientHeight;

      // Порог можно подстроить — 70px работает идеально
      setCompact(h < 70);
    });

    observer.observe(el);
    return () => observer.disconnect();
  }, []);

  return (
    <div
      ref={ref}
      style={{
        backgroundColor: 'var(--colorNeutralBackground3)',
        border: '1px solid var(--colorNeutralStroke1)',
        borderRadius: '6px',
        padding: '12px',
        minWidth: '120px',
        textAlign: 'center',
        display: 'flex',
        flexDirection: 'column',
        gap: '4px',
        overflow: 'hidden',
        flexShrink: 1,
        minHeight: 0,
      }}
    >
      {!compact && (
        <Text size={100} style={{ color: 'var(--colorNeutralForeground3)' }}>
          Altitude
        </Text>
      )}

      <Text size={300} weight="bold" style={{ color: 'var(--colorBrandForeground1)' }}>
        {altitude !== null ? `${altitude.toFixed(1)} m` : '—'}
      </Text>
    </div>
  );
};
