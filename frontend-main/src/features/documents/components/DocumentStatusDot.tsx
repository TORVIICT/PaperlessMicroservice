import { cn } from '@/lib/utils';

import {
  getDocumentStatusPresentation,
  type DocumentStatusTone,
} from '../lib/statusLabels';
import type { DocumentStatus } from '../models/document';

const DOT_CLASSES: Record<DocumentStatusTone, string> = {
  neutral: 'bg-status-queued/60',
  info: 'bg-status-info',
  warning: 'bg-status-processing',
  success: 'bg-status-completed',
  danger: 'bg-status-error',
};

interface DocumentStatusDotProps {
  status: DocumentStatus;
  className?: string;
}

export function DocumentStatusDot({
  status,
  className,
}: DocumentStatusDotProps) {
  const { tone } = getDocumentStatusPresentation(status);

  return (
    <span
      aria-hidden="true"
      className={cn(
        'size-1.5 shrink-0 rounded-full',
        DOT_CLASSES[tone],
        className,
      )}
    />
  );
}
