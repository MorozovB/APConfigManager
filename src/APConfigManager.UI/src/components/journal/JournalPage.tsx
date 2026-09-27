import { useState } from 'react';
import {
    Text,
    Button,
    Card,
    Spinner,
    Table,
    TableHeader,
    TableRow,
    TableHeaderCell,
    TableBody,
    TableCell,
} from '@fluentui/react-components';
import { ArrowClockwiseRegular, DeleteRegular } from '@fluentui/react-icons';
import { useTranslation } from 'react-i18next';
import { useJournal } from '../../hooks/useJournal';
import { ConfirmDialog } from '../common/ConfirmDialog';

export const JournalPage = () => {
    const { t } = useTranslation();
    const { entries, loading, error, reload, clear } = useJournal();
    const [confirmOpen, setConfirmOpen] = useState(false);

    const handleClear = async () => {
        setConfirmOpen(false);
        await clear();
    };

    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                <Text size={500} weight="semibold">{t('journal.title')}</Text>
                <Button appearance="subtle" icon={<ArrowClockwiseRegular />} onClick={reload} disabled={loading}>
                    {t('journal.refresh')}
                </Button>
                <Button appearance="subtle" icon={<DeleteRegular />} onClick={() => setConfirmOpen(true)}
                        disabled={loading || entries.length === 0} style={{ color: '#d63031' }}>
                    {t('journal.clear')}
                </Button>
                {loading && <Spinner size="tiny" />}
            </div>

            {error && <Text size={200} style={{ color: '#ff7675' }}>{error}</Text>}

            {!loading && !error && entries.length === 0 && (
                <Text size={200} style={{ color: 'var(--colorNeutralForeground3)' }}>
                    {t('journal.empty')}
                </Text>
            )}

            {entries.length > 0 && (
                <Card style={{ padding: 0, overflow: 'auto', backgroundColor: 'var(--colorNeutralBackground2)' }}>
                    <Table size="small" aria-label={t('journal.title')}>
                        <TableHeader>
                            <TableRow>
                                <TableHeaderCell>{t('journal.time')}</TableHeaderCell>
                                <TableHeaderCell>{t('journal.operation')}</TableHeaderCell>
                                <TableHeaderCell>{t('journal.port')}</TableHeaderCell>
                                <TableHeaderCell>{t('journal.serial')}</TableHeaderCell>
                                <TableHeaderCell>{t('journal.profile')}</TableHeaderCell>
                                <TableHeaderCell>{t('journal.result')}</TableHeaderCell>
                                <TableHeaderCell>{t('journal.message')}</TableHeaderCell>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {entries.map((e) => (
                                <TableRow key={e.id}>
                                    <TableCell>{new Date(e.timestampUtc).toLocaleString()}</TableCell>
                                    <TableCell>{e.operation}</TableCell>
                                    <TableCell>{e.port}</TableCell>
                                    <TableCell>{e.deviceSerial}</TableCell>
                                    <TableCell>{e.profileName}</TableCell>
                                    <TableCell>
                                        <Text weight="semibold" style={{ color: e.success ? '#00b894' : '#ff7675' }}>
                                            {e.success ? t('journal.ok') : t('journal.fail')}
                                        </Text>
                                    </TableCell>
                                    <TableCell>{e.message}</TableCell>
                                </TableRow>
                            ))}
                        </TableBody>
                    </Table>
                </Card>
            )}

            <ConfirmDialog
                open={confirmOpen}
                title={t('journal.clearTitle')}
                message={t('journal.clearMessage')}
                confirmText={t('journal.clear')}
                onConfirm={handleClear}
                onCancel={() => setConfirmOpen(false)}
            />
        </div>
    );
};