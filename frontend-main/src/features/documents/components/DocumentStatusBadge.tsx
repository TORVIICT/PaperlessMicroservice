import { cn } from '@/lib/utils';

import {
  getDocumentStatusPresentation,
  type DocumentStatusTone,
} from '../lib/statusLabels';
import type { DocumentStatus } from '../models/document';
import { DocumentStatusDot } from './DocumentStatusDot';

const TONE_CLASSES: Record<DocumentStatusTone, string> = {
  neutral: 'border-border bg-muted text-muted-foreground',
  info: 'border-status-info/20 bg-status-info/10 text-status-info-foreground',
  warning:
    'border-status-processing/25 bg-status-processing/10 text-status-processing-foreground',
  success:
    'border-status-completed/25 bg-status-completed/10 text-status-completed-foreground',
  danger:
    'border-status-error/25 bg-status-error/10 text-status-error-foreground',
};

interface DocumentStatusBadgeProps {
  status: DocumentStatus;
  className?: string;
}

export function DocumentStatusBadge({
  status,
  className,
}: DocumentStatusBadgeProps) {
  const { label, description, tone } = getDocumentStatusPresentation(status);

  return (
    <span
      title={description}
      className={cn(
        'inline-flex w-fit items-center gap-1.5 rounded-full border px-2 py-0.5 text-xs font-medium',
        TONE_CLASSES[tone],
        className,
      )}
    >
      <DocumentStatusDot status={status} />
      {label}
    </span>
  );
}
