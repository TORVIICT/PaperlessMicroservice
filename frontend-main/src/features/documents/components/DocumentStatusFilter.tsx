import { cn } from '@/lib/utils';

import {
  DOCUMENT_STATUSES,
  type DocumentStatus,
} from '../models/document';
import { getDocumentStatusPresentation } from '../lib/statusLabels';

interface DocumentStatusFilterProps {
  value?: DocumentStatus;
  onChange: (status: DocumentStatus | undefined) => void;
}

const FILTER_OPTIONS: ReadonlyArray<{
  value?: DocumentStatus;
  label: string;
}> = [
  { label: 'Todos' },
  ...DOCUMENT_STATUSES.map((status) => ({
    value: status,
    label: getDocumentStatusPresentation(status).label,
  })),
];

export function DocumentStatusFilter({
  value,
  onChange,
}: DocumentStatusFilterProps) {
  return (
    <div className="flex flex-wrap items-center gap-2">
      <div
        role="group"
        aria-label="Filtrar por estado"
        className="inline-flex flex-wrap items-center gap-1 rounded-xl border bg-card p-1 shadow-xs"
      >
        {FILTER_OPTIONS.map((option) => {
          const isSelected = option.value === value;

          return (
            <button
              key={option.value ?? 'all'}
              type="button"
              aria-pressed={isSelected}
              className={cn(
                'min-h-8 rounded-lg px-3 text-sm font-medium text-muted-foreground transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring',
                isSelected
                  ? 'bg-primary text-primary-foreground shadow-sm'
                  : 'hover:bg-muted hover:text-foreground',
              )}
              onClick={() => onChange(option.value)}
            >
              {option.label}
            </button>
          );
        })}
      </div>
    </div>
  );
}
