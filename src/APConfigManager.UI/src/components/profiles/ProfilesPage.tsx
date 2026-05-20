import { useState, useCallback } from 'react';
import { Button, Text } from '@fluentui/react-components';
import { AddRegular } from '@fluentui/react-icons';
import { useProfiles } from '../../hooks/useProfiles';
import { DeviceProfile } from '../../types/profile';
import { ProfileList } from './ProfileList';
import { ProfileEditor } from './ProfileEditor';
import { ConfirmDialog } from '../common/ConfirmDialog';

export const ProfilesPage = () => {
  const { profiles, loading, error, save, remove } = useProfiles();

  const [editorOpen, setEditorOpen] = useState(false);
  const [editingProfile, setEditingProfile] = useState<DeviceProfile | null>(null);

  const [deleteConfirmOpen, setDeleteConfirmOpen] = useState(false);

  const [deletingProfileId, setDeletingProfileId] = useState<string | null>(null);

  const handleCreate = useCallback(() => {
    setEditingProfile(null);     // null = новый профиль
    setEditorOpen(true);
  }, []);

  const handleEdit = useCallback((profile: DeviceProfile) => {
    setEditingProfile(profile);  // передаём данные профиля
    setEditorOpen(true);
  }, []);

  const handleSave = useCallback(async (profile: DeviceProfile) => {
    await save(profile);
    setEditorOpen(false);
    setEditingProfile(null);
  }, [save]);

  const handleCancel = useCallback(() => {
    setEditorOpen(false);
    setEditingProfile(null);
  }, []);

  const handleDeleteRequest = useCallback((profileId: string) => {
    setDeletingProfileId(profileId);
    setDeleteConfirmOpen(true);
  }, []);

  const handleDeleteConfirm = useCallback(async () => {
    if (deletingProfileId) {
      await remove(deletingProfileId);
    }
    setDeleteConfirmOpen(false);
    setDeletingProfileId(null);
  }, [deletingProfileId, remove]);

  const handleDeleteCancel = useCallback(() => {
    setDeleteConfirmOpen(false);
    setDeletingProfileId(null);
  }, []);

  const deletingProfileName = profiles.find(p => p.id === deletingProfileId)?.name || '';

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>

      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Text size={500} weight="semibold">
          Device Profiles
        </Text>
        <Button
          appearance="primary"
          icon={<AddRegular />}
          onClick={handleCreate}
        >
          New Profile
        </Button>
      </div>

      {error && (
        <Text size={200} style={{ color: '#ff7675' }}>{error}</Text>
      )}

      {loading && (
        <Text size={200} style={{ color: 'var(--colorNeutralForeground3)' }}>
          Loading profiles...
        </Text>
      )}

      <ProfileList
        profiles={profiles}
        onEdit={handleEdit}
        onDelete={handleDeleteRequest}
      />

      <ProfileEditor
        open={editorOpen}
        profile={editingProfile}
        onSave={handleSave}
        onCancel={handleCancel}
      />

      <ConfirmDialog
        open={deleteConfirmOpen}
        title="Delete Profile"
        message={`Are you sure you want to delete "${deletingProfileName}"? This action cannot be undone.`}
        confirmText="Delete"
        onConfirm={handleDeleteConfirm}
        onCancel={handleDeleteCancel}
      />
    </div>
  );
};
