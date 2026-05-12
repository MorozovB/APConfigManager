import { Text } from '@fluentui/react-components';
import { ShieldCheckmarkRegular } from '@fluentui/react-icons';

export const AppHeader = () => {
  return (
    <div style={{
      display: 'flex',
      alignItems: 'center',
      padding: '8px 16px',
      backgroundColor: 'var(--colorNeutralBackground3)',
      borderBottom: '1px solid var(--colorNeutralStroke1)',
      gap: '10px'
    }}>
      {/* Logo */}
      <ShieldCheckmarkRegular
        style={{ fontSize: '28px', color: 'var(--colorBrandForeground1)' }}
      />

      {/* Title */}
      <Text size={500} weight="semibold">
        AP Configuration Manager
      </Text>
    </div>
  );
};
