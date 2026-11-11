import {
  formatContentType,
  formatDocumentDate,
  formatFileSize,
  formatOptionalDocumentDate,
  formatPageCount,
} from '../../lib/documentFormatters';
import type { OcrDocument } from '../../models/document';

interface DocumentMetadataProps {
  document: OcrDocument;
}

export function DocumentMetadata({ document }: DocumentMetadataProps) {
  return (
    <section className="rounded-xl border bg-card p-5 text-card-foreground shadow-xs">
      <h2 className="text-lg font-semibold">Información del documento</h2>
      <dl className="mt-4 grid gap-x-6 gap-y-5 sm:grid-cols-2">
        <div className="space-y-1">
          <dt className="text-xs font-medium tracking-wide text-muted-foreground uppercase">
            Formato
          </dt>
          <dd className="text-sm">{formatContentType(document.contentType)}</dd>
        </div>
        <div className="space-y-1">
          <dt className="text-xs font-medium tracking-wide text-muted-foreground uppercase">
            Tamaño
          </dt>
          <dd className="text-sm">{formatFileSize(document.sizeBytes)}</dd>
        </div>
        <div className="space-y-1">
          <dt className="text-xs font-medium tracking-wide text-muted-foreground uppercase">
            Creado
          </dt>
          <dd className="text-sm">
            <time dateTime={document.createdAt}>
              {formatDocumentDate(document.createdAt)}
            </time>
          </dd>
        </div>
        <div className="space-y-1">
          <dt className="text-xs font-medium tracking-wide text-muted-foreground uppercase">
            Finalizado
          </dt>
          <dd className="text-sm">
            {formatOptionalDocumentDate(document.completedAt)}
          </dd>
        </div>
        <div className="space-y-1">
          <dt className="text-xs font-medium tracking-wide text-muted-foreground uppercase">
            Páginas
          </dt>
          <dd className="text-sm">{formatPageCount(document.pageCount)}</dd>
        </div>
      </dl>
    </section>
  );
}
