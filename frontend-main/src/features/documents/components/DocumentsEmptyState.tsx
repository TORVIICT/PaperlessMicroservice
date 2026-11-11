import { Inbox } from 'lucide-react';

import { Button } from '@/components/ui/button';

import { getDocumentStatusPresentation } from '../lib/statusLabels';
import type { DocumentStatus } from '../models/document';

interface DocumentsEmptyStateProps {
  status?: DocumentStatus;
  onClearFilter: () => void;
}

export function DocumentsEmptyState({
  status,
  onClearFilter,
}: DocumentsEmptyStateProps) {
  const statusLabel = status
    ? getDocumentStatusPresentation(status).label
    : null;

  return (
    <section
      role="status"
      className="rounded-xl border border-dashed bg-card px-6 py-14 text-center shadow-xs"
    >
      <span
        aria-hidden="true"
        className="mx-auto mb-4 grid size-11 place-items-center rounded-full bg-muted text-muted-foreground"
      >
        <Inbox className="size-5" />
      </span>
      <h2 className="text-lg font-semibold">
        {statusLabel
          ? `No hay documentos en estado “${statusLabel}”`
          : 'Todavía no hay documentos'}
      </h2>
      <p className="mx-auto mt-2 max-w-md text-sm text-muted-foreground">
        {statusLabel
          ? 'Probá con otro estado o quitá el filtro para ver todos los documentos.'
          : 'Cuando cargues un documento, aparecerá acá con su estado de procesamiento.'}
      </p>
      {statusLabel ? (
        <Button
          type="button"
          variant="outline"
          size="sm"
          className="mt-5"
          onClick={onClearFilter}
        >
          Limpiar filtro
        </Button>
      ) : null}
    </section>
  );
}
