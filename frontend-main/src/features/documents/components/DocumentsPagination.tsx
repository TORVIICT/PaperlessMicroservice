import { ChevronLeft, ChevronRight } from 'lucide-react';

import { Button } from '@/components/ui/button';

import { getPageCount, getPageRange } from '../lib/pagination';

interface DocumentsPaginationProps {
  page: number;
  pageSize: number;
  total: number;
  onPageChange: (page: number) => void;
}

export function DocumentsPagination({
  page,
  pageSize,
  total,
  onPageChange,
}: DocumentsPaginationProps) {
  const pageCount = getPageCount(total, pageSize);
  const range = getPageRange(page, pageSize, total);

  if (!range) {
    return null;
  }

  const currentPage = Math.min(Math.max(page, 1), pageCount);

  return (
    <nav
      aria-label="Paginación de documentos"
      className="flex flex-col gap-3 rounded-xl border bg-card px-4 py-3 shadow-xs sm:flex-row sm:items-center sm:justify-between"
    >
      <p className="text-sm text-muted-foreground">
        {range.from}–{range.to} de {total}{' '}
        {total === 1 ? 'documento' : 'documentos'}
      </p>

      {pageCount > 1 ? (
        <div className="flex items-center gap-2">
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => onPageChange(currentPage - 1)}
            disabled={currentPage <= 1}
          >
            <ChevronLeft aria-hidden="true" />
            Anterior
          </Button>
          <span className="min-w-24 text-center text-sm text-muted-foreground">
            Página {currentPage} de {pageCount}
          </span>
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => onPageChange(currentPage + 1)}
            disabled={currentPage >= pageCount}
          >
            Siguiente
            <ChevronRight aria-hidden="true" />
          </Button>
        </div>
      ) : null}
    </nav>
  );
}
