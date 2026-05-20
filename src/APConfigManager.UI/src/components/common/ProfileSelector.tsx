import { Dropdown, Option } from '@fluentui/react-components';
import { DeviceProfile } from '../../types/profile';

interface Props {
  profiles: DeviceProfile[];
  selectedProfileId: string | null;
  onSelect: (profileId: string) => void;
  disabled?: boolean;
}

export const ProfileSelector = ({ profiles, selectedProfileId, onSelect, disabled }: Props) => {
  const selected = profiles.find(p => p.id === selectedProfileId);

  return (
    <Dropdown
      placeholder="Select profile"
      value={selected?.name || ''}
      selectedOptions={selectedProfileId ? [selectedProfileId] : []}
      onOptionSelect={(_event, data) => {
        if (data.optionValue) {
          onSelect(data.optionValue);
        }
      }}
      disabled={disabled}
      style={{ minWidth: '200px' }}
    >
      {profiles.map((profile) => (
        <Option key={profile.id} value={profile.id} text={profile.name}>
          {profile.name}
        </Option>
      ))}
    </Dropdown>
  );
};
