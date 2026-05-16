import { useRef, useEffect, useState } from 'react';
import { Text } from '@fluentui/react-components';

interface Props {
  data: { x: number; y: number; z: number }[];
}

export const AccelerometerWidget = ({ data }: Props) => {
  const canvasRef = useRef<HTMLCanvasElement>(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;

    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    const width = canvas.width;
    const height = canvas.height;

    ctx.fillStyle = '#0a2a0a';
    ctx.fillRect(0, 0, width, height);

    ctx.strokeStyle = '#1a4a1a';
    ctx.lineWidth = 0.5;
    for (let y = 0; y < height; y += 20) {
      ctx.beginPath();
      ctx.moveTo(0, y);
      ctx.lineTo(width, y);
      ctx.stroke();
    }

    if (data.length < 2) return;

    const scale = height / 20;
    const step = width / (data.length - 1);

    ctx.strokeStyle = '#ff4444';
    ctx.lineWidth = 1.5;
    ctx.beginPath();
    data.forEach((point, i) => {
      const x = i * step;
      const y = height / 2 - point.x * scale;
      if (i === 0) ctx.moveTo(x, y);
      else ctx.lineTo(x, y);
    });
    ctx.stroke();

    ctx.strokeStyle = '#44ff44';
    ctx.beginPath();
    data.forEach((point, i) => {
      const x = i * step;
      const y = height / 2 - point.y * scale;
      if (i === 0) ctx.moveTo(x, y);
      else ctx.lineTo(x, y);
    });
    ctx.stroke();

    ctx.strokeStyle = '#4444ff';
    ctx.beginPath();
    data.forEach((point, i) => {
      const x = i * step;
      const y = height / 2 - point.z * scale;
      if (i === 0) ctx.moveTo(x, y);
      else ctx.lineTo(x, y);
    });
    ctx.stroke();
  }, [data]);

  return (
    <div style={{
      backgroundColor: 'var(--colorNeutralBackground3)',
      border: '1px solid var(--colorNeutralStroke1)',
      borderRadius: '6px',
      padding: '8px',
      display: 'flex',
      flexDirection: 'column',
      gap: '4px',
    }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Text size={200} style={{ color: 'var(--colorNeutralForeground3)' }}>IMU</Text>
        <div style={{ display: 'flex', gap: '8px', fontSize: '10px' }}>
          <span style={{ color: '#ff4444' }}>X</span>
          <span style={{ color: '#44ff44' }}>Y</span>
          <span style={{ color: '#4444ff' }}>Z</span>
        </div>
      </div>
      <canvas
        ref={canvasRef}
        width={200}
        height={100}
        style={{ borderRadius: '4px' }}
      />
    </div>
  );
};

export const useMockAccelerometer = () => {
  const [data, setData] = useState<{ x: number; y: number; z: number }[]>([]);

  useEffect(() => {
    const interval = setInterval(() => {
      setData(prev => {
        const newPoint = {
          x: Math.sin(Date.now() / 500) * 2 + (Math.random() - 0.5) * 0.5,
          y: Math.cos(Date.now() / 700) * 1.5 + (Math.random() - 0.5) * 0.3,
          z: 9.8 + (Math.random() - 0.5) * 0.2,
        };
        const updated = [...prev, newPoint];
        return updated.slice(-100);
      });
    }, 100);

    return () => clearInterval(interval);
  }, []);

  return data;
};
