import {
    Dialog,
    DialogSurface,
    DialogBody,
    DialogTitle,
    DialogContent,
    DialogActions,
    Button,
} from '@fluentui/react-components';
import {useTranslation} from "react-i18next";

interface Props {
    open: boolean;
    title: string;
    message: string;
    confirmText?: string;
    onConfirm: () => void;
    onCancel: () => void;
}

export const ConfirmDialog = ({
                                  open,
                                  title,
                                  message,
                                  confirmText,
                                  onConfirm,
                                  onCancel,
                              }: Props) => {
    const {t} = useTranslation();
    return (
        <Dialog open={open} onOpenChange={(_e, data) => {
            if (!data.open) onCancel();
        }}>
            <DialogSurface>
                <DialogBody>
                    <DialogTitle>{title}</DialogTitle>
                    <DialogContent>{message}</DialogContent>
                    <DialogActions>
                        <Button appearance="secondary" onClick={onCancel}>
                            {t('common.cancel')}
                        </Button>
                        <Button appearance="primary" onClick={onConfirm}
                                style={{backgroundColor: '#d63031', borderColor: '#d63031'}}>
                            {confirmText ?? t('common.confirm')}
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};
