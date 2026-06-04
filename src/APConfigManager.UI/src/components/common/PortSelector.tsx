import { Dropdown, Option } from '@fluentui/react-components';
import { PortInfo } from '../../types/device.ts';

interface Props {
  ports: PortInfo[];
  selectedPort: string;
  onSelect: (port: string) => void;
  disabled?: boolean;
}

export const PortSelector = ({ ports, selectedPort, onSelect, disabled }: Props) => {
  return (
    <Dropdown
      placeholder="Select port"
      value={selectedPort ? ports.find(p => p.name === selectedPort)
          ? `${selectedPort} — ${ports.find(p => p.name === selectedPort)?.description || ''}`
          : selectedPort
        : ''}
      selectedOptions={selectedPort ? [selectedPort] : []}
      onOptionSelect={(_event, data) => {
        if (data.optionValue) {
          onSelect(data.optionValue);
        }
      }}
      disabled={disabled}
      style={{ minWidth: '250px' }}
    >
      {ports.map((port) => (
        <Option key={port.name} value={port.name} text={port.name}>
          {port.name} {port.description ? `— ${port.description}` : ''}
        </Option>
      ))}
    </Dropdown>
  );
};
