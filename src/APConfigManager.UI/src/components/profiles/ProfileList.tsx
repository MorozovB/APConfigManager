import {
  Table,
  TableHeader,
  TableRow,
  TableHeaderCell,
  TableBody,
  TableCell,
  Button,
  Badge,
  Text,
} from '@fluentui/react-components';
import { EditRegular, DeleteRegular } from '@fluentui/react-icons';
import { DeviceProfile } from '../../types/profile';

interface Props {
  profiles: DeviceProfile[];
  onEdit: (profile: DeviceProfile) => void;
  onDelete: (profileId: string) => void;
}

export const ProfileList = ({ profiles, onEdit, onDelete }: Props) => {
  if (profiles.length === 0) {
    return (
      <div style={{
        textAlign: 'center',
        padding: '40px',
        color: 'var(--colorNeutralForeground3)',
      }}>
        <Text size={400}>No profiles yet. Create one to get started.</Text>
      </div>
    );
  }

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHeaderCell style={{ minWidth: '150px' }}>Name</TableHeaderCell>
          <TableHeaderCell style={{ minWidth: '200px' }}>Description</TableHeaderCell>
          <TableHeaderCell style={{ minWidth: '150px' }}>Firmware</TableHeaderCell>
          <TableHeaderCell style={{ minWidth: '150px' }}>Parameters</TableHeaderCell>
          <TableHeaderCell style={{ minWidth: '180px' }}>Operations</TableHeaderCell>
          <TableHeaderCell style={{ minWidth: '100px' }}>Actions</TableHeaderCell>
        </TableRow>
      </TableHeader>
      <TableBody>
        {profiles.map((profile) => (
          <TableRow key={profile.id}>
            <TableCell>
              <Text weight="semibold">{profile.name}</Text>
            </TableCell>

            <TableCell>
              <Text size={200} style={{ color: 'var(--colorNeutralForeground3)' }}>
                {profile.description || '—'}
              </Text>
            </TableCell>

            <TableCell>
              <Text size={200}>
                {profile.firmwareFileName || '—'}
              </Text>
            </TableCell>

            <TableCell>
              <Text size={200}>
                {profile.parameterFileName || '—'}
              </Text>
            </TableCell>

            <TableCell>
              <div style={{ display: 'flex', gap: '4px', flexWrap: 'wrap' }}>
                {profile.profileOptions.bootloader && (
                  <Badge appearance="outline" color="informative" size="small">BL</Badge>
                )}
                {profile.profileOptions.firmware && (
                  <Badge appearance="outline" color="success" size="small">FW</Badge>
                )}
                {profile.profileOptions.parameters && (
                  <Badge appearance="outline" color="warning" size="small">Params</Badge>
                )}
                {!profile.profileOptions.bootloader &&
                  !profile.profileOptions.firmware &&
                  !profile.profileOptions.parameters && (
                    <Text size={200} style={{ color: 'var(--colorNeutralForeground3)' }}>None</Text>
                  )}
              </div>
            </TableCell>
            
            <TableCell>
              <div style={{ display: 'flex', gap: '4px' }}>
                <Button
                  icon={<EditRegular />}
                  appearance="subtle"
                  size="small"
                  onClick={() => onEdit(profile)}
                />
                <Button
                  icon={<DeleteRegular />}
                  appearance="subtle"
                  size="small"
                  onClick={() => onDelete(profile.id)}
                  style={{ color: '#d63031' }}
                />
              </div>
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
};
