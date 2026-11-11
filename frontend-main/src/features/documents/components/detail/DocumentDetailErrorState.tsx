import { AlertTriangle, RefreshCw } from 'lucide-react';

import { Button } from '@/components/ui/button';

import { toUserFacingError } from '../../lib/errorMessages';

interface DocumentDetailErrorStateProps {
  error: unknown;
  onRetry: () => void;
}

export function DocumentDetailErrorState({
  error,
  onRetry,
}: DocumentDetailErrorStateProps) {
  const { code, message } = toUserFacingError(error);
  const isNotFound = code === 'DOCUMENT_NOT_FOUND';

  return (
    <section
      role="alert"
      className="rounded-xl border border-destructive/20 bg-destructive/5 p-6"
    >
      <div className="flex flex-col gap-4 sm:flex-row sm:items-start">
        <span
          aria-hidden="true"
          className="grid size-10 shrink-0 place-items-center rounded-full bg-destructive/10 text-destructive"
        >
          <AlertTriangle className="size-5" />
        </span>
        <div className="space-y-2">
          <h1 className="text-lg font-semibold">
            {isNotFound
              ? 'Documento no encontrado'
              : 'No pudimos cargar el documento'}
          </h1>
          <p className="text-sm text-muted-foreground">{message}</p>
          <p className="text-xs text-muted-foreground">
            Código:{' '}
            <code className="rounded bg-muted px-1.5 py-0.5 font-mono">
              {code}
            </code>
          </p>
          {!isNotFound ? (
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={onRetry}
            >
              <RefreshCw aria-hidden="true" />
              Reintentar
            </Button>
          ) : null}
        </div>
      </div>
    </section>
  );
}
