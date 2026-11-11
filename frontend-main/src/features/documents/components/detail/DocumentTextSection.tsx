import { Loader2, RefreshCw } from 'lucide-react';

import { Button } from '@/components/ui/button';

import { isDocumentReadyForResults } from '../../lib/documentAvailability';
import { toUserFacingError } from '../../lib/errorMessages';
import { getDocumentStatusPresentation } from '../../lib/statusLabels';
import type {
  DocumentStatus,
  DocumentTextResult,
} from '../../models/document';
import { ErrorAlert } from '../ErrorAlert';

interface DocumentTextSectionProps {
  status: DocumentStatus;
  text: DocumentTextResult | undefined;
  isLoading: boolean;
  error: unknown;
  onRetry: () => void;
}

export function DocumentTextSection({
  status,
  text,
  isLoading,
  error,
  onRetry,
}: DocumentTextSectionProps) {
  const isReady = isDocumentReadyForResults(status);
  const presentation = getDocumentStatusPresentation(status);
  const textError = error ? toUserFacingError(error) : null;

  return (
    <section className="rounded-xl border bg-card text-card-foreground shadow-xs">
      <div className="border-b px-5 py-4">
        <h2 className="text-lg font-semibold">Texto extraído</h2>
      </div>

      <div className="p-5">
        {isReady ? (
          isLoading ? (
            <div
              role="status"
              aria-label="Cargando texto extraído"
              className="animate-pulse space-y-3"
            >
              <span className="sr-only">Cargando texto extraído…</span>
              <div className="h-4 w-full rounded bg-muted" />
              <div className="h-4 w-11/12 rounded bg-muted" />
              <div className="h-4 w-4/5 rounded bg-muted" />
              <div className="h-4 w-2/3 rounded bg-muted" />
            </div>
          ) : textError ? (
            <ErrorAlert
              title="No pudimos cargar el texto"
              code={textError.code}
              message={textError.message}
              action={
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={onRetry}
                >
                  <RefreshCw aria-hidden="true" />
                  Reintentar
                </Button>
              }
            />
          ) : text && text.text.trim() ? (
            <div className="max-h-[36rem] overflow-auto rounded-lg border bg-muted/20 p-4 focus-within:ring-2 focus-within:ring-ring/30">
              <p className="break-words whitespace-pre-wrap text-sm leading-relaxed">
                {text.text}
              </p>
            </div>
          ) : (
            <p className="py-4 text-center text-sm text-muted-foreground">
              El OCR no devolvió texto para este documento.
            </p>
          )
        ) : status === 'failed' ? (
          <div className="py-6 text-center">
            <p className="font-medium">No hay texto disponible</p>
            <p className="mt-1 text-sm text-muted-foreground">
              {presentation.description}
            </p>
          </div>
        ) : (
          <div
            role="status"
            className="flex flex-col items-center py-6 text-center"
          >
            <Loader2
              aria-hidden="true"
              className="size-5 animate-spin text-muted-foreground"
            />
            <p className="mt-3 font-medium">{presentation.label}</p>
            <p className="mt-1 max-w-md text-sm text-muted-foreground">
              {presentation.description} El texto aparecerá automáticamente
              cuando finalice el procesamiento.
            </p>
          </div>
        )}
      </div>
    </section>
  );
}
