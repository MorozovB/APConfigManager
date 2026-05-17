import { Button, Text, Card, CardHeader } from '@fluentui/react-components';

interface Props {
  icon: React.ReactNode;
  title: string;
  description: string;
  buttonText: string;
  buttonColor?: string;
  onClick: () => void;
  disabled?: boolean;
  loading?: boolean;
  statusMessage?: string | null;
  statusType?: 'success' | 'error' | 'info';
}

const statusColors = {
  success: '#00b894',
  error: '#ff7675',
  info: 'var(--colorNeutralForeground3)',
};

export const ToolCard = ({
                           icon,
                           title,
                           description,
                           buttonText,
                           buttonColor,
                           onClick,
                           disabled = false,
                           loading = false,
                           statusMessage = null,
                           statusType = 'info',
                         }: Props) => {
  return (
    <Card style={{
      padding: '20px',
      backgroundColor: 'var(--colorNeutralBackground2)',
      border: '1px solid var(--colorNeutralStroke1)',
    }}>
      <CardHeader
        image={
          <div style={{
            fontSize: '28px',
            color: 'var(--colorBrandForeground1)',
            display: 'flex',
            alignItems: 'center',
          }}>
            {icon}
          </div>
        }
        header={<Text weight="semibold" size={400}>{title}</Text>}
        description={
          <Text size={200} style={{ color: 'var(--colorNeutralForeground3)' }}>
            {description}
          </Text>
        }
      />

      <div style={{
        display: 'flex',
        alignItems: 'center',
        gap: '12px',
        marginTop: '16px',
      }}>
        <Button
          appearance="primary"
          onClick={onClick}
          disabled={disabled || loading}
          style={buttonColor ? { backgroundColor: buttonColor, borderColor: buttonColor } : undefined}
        >
          {loading ? 'Processing...' : buttonText}
        </Button>

        {statusMessage && (
          <Text size={200} style={{ color: statusColors[statusType] }}>
            {statusMessage}
          </Text>
        )}
      </div>
    </Card>
  );
};
