import { Loader2, Trash2 } from 'lucide-react';

import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';

import { toUserFacingError } from '../../lib/errorMessages';
import type { OcrDocument } from '../../models/document';
import { ErrorAlert } from '../ErrorAlert';

interface DeleteDocumentDialogProps {
  document: OcrDocument;
  open: boolean;
  isDeleting: boolean;
  error: unknown;
  onOpenChange: (open: boolean) => void;
  onConfirm: () => void;
}

export function DeleteDocumentDialog({
  document,
  open,
  isDeleting,
  error,
  onOpenChange,
  onConfirm,
}: DeleteDocumentDialogProps) {
  const deleteError = error ? toUserFacingError(error) : null;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent
        className="sm:max-w-md"
        showCloseButton={!isDeleting}
      >
        <DialogHeader>
          <DialogTitle>Eliminar documento</DialogTitle>
          <DialogDescription>
            Se eliminarán el documento «{document.fileName}» y su texto
            extraído. Esta acción no se puede deshacer.
          </DialogDescription>
        </DialogHeader>

        {deleteError ? (
          <ErrorAlert
            title="No pudimos eliminar el documento"
            code={deleteError.code}
            message={deleteError.message}
          />
        ) : null}

        <DialogFooter>
          <Button
            type="button"
            variant="outline"
            onClick={() => onOpenChange(false)}
            disabled={isDeleting}
          >
            Cancelar
          </Button>
          <Button
            type="button"
            variant="destructive"
            onClick={onConfirm}
            disabled={isDeleting}
          >
            {isDeleting ? (
              <>
                <Loader2 className="animate-spin" aria-hidden="true" />
                Eliminando…
              </>
            ) : (
              <>
                <Trash2 aria-hidden="true" />
                {deleteError ? 'Reintentar' : 'Eliminar'}
              </>
            )}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
