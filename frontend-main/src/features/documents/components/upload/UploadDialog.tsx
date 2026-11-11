import { useState } from 'react';
import { Loader2 } from 'lucide-react';

import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';

import { useUploadDocumentsMutation } from '../../hooks/useUploadDocumentsMutation';
import { getUserFacingMessage } from '../../lib/errorMessages';
import { validateDocumentFile } from '../../lib/fileValidation';
import { ErrorAlert } from '../ErrorAlert';
import { DocumentDropzone } from './DocumentDropzone';
import { SelectedDocumentFile } from './SelectedDocumentFile';
import { UploadBatchResult } from './UploadBatchResult';

interface UploadDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

function fileIdentity(file: File): string {
  return `${file.name}-${file.size}-${file.lastModified}`;
}

export function UploadDialog({ open, onOpenChange }: UploadDialogProps) {
  const [selectedFiles, setSelectedFiles] = useState<File[]>([]);
  const mutation = useUploadDocumentsMutation();
  const isUploading = mutation.isPending;
  const hasResults = mutation.items.length > 0;
  const failedItems = mutation.items.filter((item) => item.status === 'error');
  const invalidFile = selectedFiles.find((file) => validateDocumentFile(file));
  const validationCode = invalidFile ? validateDocumentFile(invalidFile) : null;

  function resetForm() {
    setSelectedFiles([]);
    mutation.reset();
  }

  function handleOpenChange(nextOpen: boolean) {
    if (!nextOpen && isUploading) return;

    if (!nextOpen) resetForm();
    onOpenChange(nextOpen);
  }

  function handleFilesSelected(files: File[]) {
    setSelectedFiles((current) => {
      const knownFiles = new Set(current.map(fileIdentity));
      return [
        ...current,
        ...files.filter((file) => !knownFiles.has(fileIdentity(file))),
      ];
    });
    mutation.reset();
  }

  function handleClearFile(fileToRemove: File) {
    setSelectedFiles((current) =>
      current.filter((file) => file !== fileToRemove),
    );
  }

  function handleUpload() {
    if (selectedFiles.length === 0 || validationCode || isUploading) return;
    mutation.mutate(selectedFiles);
  }

  function handleRetryFailed() {
    const files = failedItems.map((item) => item.file);
    if (files.length > 0) mutation.mutate(files);
  }

  const title = hasResults
    ? isUploading
      ? 'Subiendo documentos'
      : 'Carga finalizada'
    : 'Cargar documentos';

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="gap-5 sm:max-w-xl" showCloseButton={!isUploading}>
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>
            {hasResults
              ? 'Cada archivo se envía como un documento independiente.'
              : 'Seleccioná uno o varios archivos. Se subirán hasta 3 al mismo tiempo.'}
          </DialogDescription>
        </DialogHeader>

        {hasResults ? (
          <>
            <UploadBatchResult items={mutation.items} />
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => handleOpenChange(false)} disabled={isUploading}>
                Cerrar
              </Button>
              {!isUploading && failedItems.length > 0 ? (
                <Button type="button" onClick={handleRetryFailed}>Reintentar fallidos</Button>
              ) : !isUploading ? (
                <Button type="button" onClick={resetForm}>Cargar otros</Button>
              ) : null}
            </DialogFooter>
          </>
        ) : (
          <>
            <DocumentDropzone disabled={isUploading} onFilesSelected={handleFilesSelected} />

            {selectedFiles.length > 0 ? (
              <div className="max-h-56 space-y-2 overflow-y-auto" aria-label="Archivos seleccionados">
                {selectedFiles.map((file) => (
                  <SelectedDocumentFile
                    key={fileIdentity(file)}
                    file={file}
                    disabled={isUploading}
                    onClear={() => handleClearFile(file)}
                  />
                ))}
              </div>
            ) : null}

            {validationCode && invalidFile ? (
              <ErrorAlert
                title={`Revisá ${invalidFile.name}`}
                code={validationCode}
                message={getUserFacingMessage(validationCode)}
              />
            ) : null}

            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => handleOpenChange(false)} disabled={isUploading}>
                Cancelar
              </Button>
              <Button
                type="button"
                onClick={handleUpload}
                disabled={selectedFiles.length === 0 || validationCode !== null || isUploading}
              >
                {isUploading ? (
                  <><Loader2 className="animate-spin" aria-hidden="true" />Subiendo…</>
                ) : selectedFiles.length === 1 ? (
                  'Subir documento'
                ) : (
                  `Subir ${selectedFiles.length} documentos`
                )}
              </Button>
            </DialogFooter>
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}
