import { useRef, useEffect } from 'react';

export interface LogEntry {
  timestamp: string;
  message: string;
  type: 'info' | 'success' | 'error' | 'warn' | 'progress';
}

const typeColors: Record<string, string> = {
  info: '#74b9ff',
  success: '#00b894',
  error: '#ff7675',
  warn: '#fdcb6e',
  progress: '#636e72',
};

interface Props {
  entries: LogEntry[];
  visible: boolean;
}

export const LogConsole = ({ entries, visible }: Props) => {
  const bottomRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [entries.length]);

  if (!visible) return null;

  return (
    <div style={{
      backgroundColor: '#0a0a1a',
      border: '1px solid var(--colorNeutralStroke1)',
      borderRadius: '4px',
      padding: '8px',
      height: '100%',
      overflowY: 'auto',
      fontFamily: 'Consolas, monospace',
      fontSize: '12px',
      lineHeight: '1.6',
    }}>
      {entries.map((entry, index) => (
        <div key={index} style={{ color: typeColors[entry.type] || '#e0e0e0' }}>
          [{entry.timestamp}] {entry.message}
        </div>
      ))}
      <div ref={bottomRef} />
    </div>
  );
};
