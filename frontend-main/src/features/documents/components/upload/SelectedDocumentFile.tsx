import { FileText, X } from 'lucide-react';

import { Button } from '@/components/ui/button';

import {
  formatContentType,
  formatFileSize,
} from '../../lib/documentFormatters';

interface SelectedDocumentFileProps {
  file: File;
  disabled?: boolean;
  onClear: () => void;
}

export function SelectedDocumentFile({
  file,
  disabled = false,
  onClear,
}: SelectedDocumentFileProps) {
  return (
    <div className="flex items-center gap-3 rounded-xl border bg-card p-3 shadow-xs">
      <span
        aria-hidden="true"
        className="grid size-9 shrink-0 place-items-center rounded-lg bg-muted text-muted-foreground"
      >
        <FileText className="size-4" />
      </span>
      <div className="min-w-0 flex-1">
        <p className="truncate text-sm font-medium" title={file.name}>
          {file.name}
        </p>
        <p className="text-xs text-muted-foreground">
          {formatContentType(file.type)} · {formatFileSize(file.size)}
        </p>
      </div>
      <Button
        type="button"
        variant="ghost"
        size="icon-sm"
        onClick={onClear}
        disabled={disabled}
        aria-label={`Quitar ${file.name}`}
      >
        <X aria-hidden="true" />
      </Button>
    </div>
  );
}
