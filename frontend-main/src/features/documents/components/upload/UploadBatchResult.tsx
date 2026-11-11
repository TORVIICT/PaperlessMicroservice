import { CheckCircle2, Loader2, XCircle } from 'lucide-react';

import type { UploadItem } from '../../hooks/useUploadDocumentsMutation';
import { toUserFacingError } from '../../lib/errorMessages';

interface UploadBatchResultProps {
  items: readonly UploadItem[];
}

export function UploadBatchResult({ items }: UploadBatchResultProps) {
  return (
    <div className="max-h-80 space-y-2 overflow-y-auto" aria-live="polite">
      {items.map((item) => {
        const error = item.error ? toUserFacingError(item.error) : null;

        return (
          <div
            key={item.id}
            className="flex items-start gap-3 rounded-xl border bg-card p-3"
          >
            {item.status === 'success' ? (
              <CheckCircle2 className="mt-0.5 size-5 shrink-0 text-status-completed-foreground" aria-hidden="true" />
            ) : item.status === 'error' ? (
              <XCircle className="mt-0.5 size-5 shrink-0 text-destructive" aria-hidden="true" />
            ) : (
              <Loader2 className="mt-0.5 size-5 shrink-0 animate-spin text-muted-foreground" aria-hidden="true" />
            )}
            <div className="min-w-0">
              <p className="truncate text-sm font-medium" title={item.file.name}>{item.file.name}</p>
              <p className="text-xs text-muted-foreground">
                {item.status === 'success'
                  ? 'Enviado al OCR'
                  : item.status === 'error'
                    ? error?.message
                    : item.status === 'uploading'
                      ? 'Subiendo…'
                      : 'En espera…'}
              </p>
            </div>
          </div>
        );
      })}
    </div>
  );
}
