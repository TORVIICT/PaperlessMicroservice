import type { OcrDocument } from '../../models/document';
import { isActiveDocumentStatus } from '../../models/document';
import { getDocumentStatusPresentation } from '../../lib/statusLabels';
import { AutomaticUpdateIndicator } from '../AutomaticUpdateIndicator';
import { DocumentStatusBadge } from '../DocumentStatusBadge';

interface DocumentDetailHeaderProps {
  document: OcrDocument;
}

export function DocumentDetailHeader({
  document,
}: DocumentDetailHeaderProps) {
  const presentation = getDocumentStatusPresentation(document.status);
  const isActive = isActiveDocumentStatus(document.status);

  return (
    <header className="border-b pb-5">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div className="min-w-0 space-y-2">
          <p className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">Documento</p>
          <h1 className="break-words text-2xl font-semibold tracking-tight">
            {document.fileName}
          </h1>
          <div className="flex flex-wrap items-center gap-2">
            <DocumentStatusBadge status={document.status} />
            <p className="text-sm text-muted-foreground">
              {presentation.description}
            </p>
          </div>
        </div>

        {isActive ? (
          <AutomaticUpdateIndicator className="shrink-0 sm:pt-1" />
        ) : null}
      </div>
    </header>
  );
}
