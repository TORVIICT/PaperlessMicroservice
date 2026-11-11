import { ChevronRight } from 'lucide-react';
import { Link } from 'react-router-dom';

import { Button } from '@/components/ui/button';
import { documentDetailPath } from '@/shared/routes';

import {
  formatContentType,
  formatDocumentDate,
  formatFileSize,
} from '../lib/documentFormatters';
import type { OcrDocument } from '../models/document';
import { DocumentStatusBadge } from './DocumentStatusBadge';
import { DocumentThumbnail } from './DocumentThumbnail';

interface DocumentsListItemProps {
  document: OcrDocument;
}

export function DocumentsListItem({ document }: DocumentsListItemProps) {
  return (
    <li className="group rounded-xl border bg-card p-4 shadow-xs transition-[border-color,box-shadow,transform] hover:-translate-y-0.5 hover:border-primary/25 hover:shadow-sm">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center">
        <div className="flex min-w-0 flex-1 items-center gap-4">
        <DocumentThumbnail contentType={document.contentType} />
        <div className="min-w-0">
          <p className="truncate font-medium" title={document.fileName}>
            {document.fileName}
          </p>
          <p className="mt-1 text-xs text-muted-foreground">
            {formatContentType(document.contentType)} ·{' '}
            {formatFileSize(document.sizeBytes)}
          </p>
          <time
            dateTime={document.createdAt}
            className="mt-1 block text-xs text-muted-foreground"
          >
            {formatDocumentDate(document.createdAt)}
          </time>
        </div>
      </div>

      <div className="flex items-center justify-between gap-3 sm:shrink-0">
        <DocumentStatusBadge status={document.status} />
        <Button
          asChild
          variant="ghost"
          size="sm"
          className="text-muted-foreground group-hover:text-foreground"
        >
          <Link
            to={documentDetailPath(document.id)}
            aria-label={`Ver detalle de ${document.fileName}`}
          >
            Ver detalle
            <ChevronRight aria-hidden="true" />
          </Link>
        </Button>
      </div>
      </div>
    </li>
  );
}
