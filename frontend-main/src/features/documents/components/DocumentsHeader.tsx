import { Upload } from 'lucide-react';

import { Button } from '@/components/ui/button';

import { AutomaticUpdateIndicator } from './AutomaticUpdateIndicator';

interface DocumentsHeaderProps {
  total: number;
  hasActiveDocuments: boolean;
  onUploadClick: () => void;
}

export function DocumentsHeader({
  total,
  hasActiveDocuments,
  onUploadClick,
}: DocumentsHeaderProps) {
  return (
    <header className="flex flex-col gap-4 border-b pb-5 sm:flex-row sm:items-center sm:justify-between">
      <div className="space-y-1">
        <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
          <h1 className="text-2xl font-semibold tracking-tight">Documentos</h1>
          <span className="text-sm font-medium text-muted-foreground">
            {total} {total === 1 ? 'documento' : 'documentos'}
          </span>
        </div>
        <p className="text-sm text-muted-foreground">
          Administrá y seguí el procesamiento de tus archivos.
        </p>
      </div>

      <div className="flex flex-col items-start gap-2 sm:items-end">
        <Button type="button" onClick={onUploadClick}>
          <Upload aria-hidden="true" />
          Cargar documento
        </Button>
        {hasActiveDocuments ? (
          <AutomaticUpdateIndicator />
        ) : null}
      </div>
    </header>
  );
}
