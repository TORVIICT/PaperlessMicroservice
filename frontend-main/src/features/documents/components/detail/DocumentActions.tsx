import { Download, FileText, Loader2, Trash2 } from 'lucide-react';

import { Button } from '@/components/ui/button';

import { isDocumentReadyForResults } from '../../lib/documentAvailability';
import type { DocumentDownloadVariant } from '../../lib/downloadFileName';
import type { DocumentStatus, OcrDocument } from '../../models/document';

interface DocumentActionsProps {
  document: OcrDocument;
  downloadingVariant: DocumentDownloadVariant | null;
  onDownload: (variant: DocumentDownloadVariant) => void;
  onDelete: () => void;
}

function getTextDownloadHint(status: DocumentStatus): string {
  if (isDocumentReadyForResults(status)) {
    return 'El archivo TXT contiene el texto reconocido por el OCR.';
  }

  if (status === 'failed') {
    return 'El texto no está disponible porque el procesamiento falló.';
  }

  return 'La descarga del texto se habilitará cuando finalice el procesamiento.';
}

export function DocumentActions({
  document,
  downloadingVariant,
  onDownload,
  onDelete,
}: DocumentActionsProps) {
  const isDownloading = downloadingVariant !== null;
  const textAvailable = isDocumentReadyForResults(document.status);
  const textDownloadHint = getTextDownloadHint(document.status);

  return (
    <section className="rounded-xl border bg-card p-5 text-card-foreground shadow-xs">
      <h2 className="text-lg font-semibold">Acciones</h2>

      <div className="mt-4 space-y-2">
        <Button
          type="button"
          variant="outline"
          className="w-full justify-start"
          onClick={() => onDownload('original')}
          disabled={isDownloading}
        >
          {downloadingVariant === 'original' ? (
            <Loader2 className="animate-spin" aria-hidden="true" />
          ) : (
            <Download aria-hidden="true" />
          )}
          {downloadingVariant === 'original'
            ? 'Descargando…'
            : 'Descargar original'}
        </Button>

        <Button
          type="button"
          variant="outline"
          className="w-full justify-start"
          onClick={() => onDownload('text')}
          disabled={isDownloading || !textAvailable}
          title={textDownloadHint}
        >
          {downloadingVariant === 'text' ? (
            <Loader2 className="animate-spin" aria-hidden="true" />
          ) : (
            <FileText aria-hidden="true" />
          )}
          {downloadingVariant === 'text'
            ? 'Descargando…'
            : 'Descargar TXT'}
        </Button>
      </div>

      <p className="mt-3 text-xs text-muted-foreground">{textDownloadHint}</p>

      <div className="mt-5 border-t pt-4">
        <Button
          type="button"
          variant="destructive"
          className="w-full justify-start"
          onClick={onDelete}
        >
          <Trash2 aria-hidden="true" />
          Eliminar documento
        </Button>
      </div>
    </section>
  );
}
