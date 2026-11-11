import { AlertTriangle, RefreshCw } from 'lucide-react';

import { Button } from '@/components/ui/button';

import { toUserFacingError } from '../lib/errorMessages';

interface DocumentsErrorStateProps {
  error: unknown;
  onRetry: () => void;
}

export function DocumentsErrorState({
  error,
  onRetry,
}: DocumentsErrorStateProps) {
  const { code, message } = toUserFacingError(error);

  return (
    <section
      role="alert"
      className="rounded-xl border border-destructive/25 bg-card p-6 shadow-xs"
    >
      <div className="flex flex-col gap-4 sm:flex-row sm:items-start">
        <span
          aria-hidden="true"
          className="grid size-10 shrink-0 place-items-center rounded-full bg-destructive/10 text-destructive"
        >
          <AlertTriangle className="size-5" />
        </span>
        <div className="space-y-2">
          <h2 className="text-lg font-semibold">
            No pudimos cargar los documentos
          </h2>
          <p className="text-sm text-muted-foreground">{message}</p>
          <p className="text-xs text-muted-foreground">
            Código:{' '}
            <code className="rounded bg-muted px-1.5 py-0.5 font-mono">
              {code}
            </code>
          </p>
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={onRetry}
          >
            <RefreshCw aria-hidden="true" />
            Reintentar
          </Button>
        </div>
      </div>
    </section>
  );
}
