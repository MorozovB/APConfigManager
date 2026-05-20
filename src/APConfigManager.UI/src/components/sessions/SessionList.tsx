import { SessionSection } from './SessionSection';

export const SessionList = () => {
  return (
    <div style={{
      display: 'flex',
      flexDirection: 'column',
      gap: '12px',
    }}>
      {[0, 1, 2, 3].map((index) => (
        <SessionSection key={index} index={index} />
      ))}
    </div>
  );
};
