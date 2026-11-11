import { useEffect, useState } from 'react';

import { DocumentsEmptyState } from '../components/DocumentsEmptyState';
import { DocumentsErrorState } from '../components/DocumentsErrorState';
import { DocumentsHeader } from '../components/DocumentsHeader';
import { DocumentsList } from '../components/DocumentsList';
import { DocumentsPagination } from '../components/DocumentsPagination';
import { DocumentsSkeleton } from '../components/DocumentsSkeleton';
import { DocumentStatusFilter } from '../components/DocumentStatusFilter';
import { UploadDialog } from '../components/upload/UploadDialog';
import { useDocumentListParams } from '../hooks/useDocumentListParams';
import { useDocumentsQuery } from '../hooks/useDocumentsQuery';
import {
  DOCUMENTS_PAGE_SIZE,
  toDocumentListParams,
} from '../lib/listParams';
import { getPageCount } from '../lib/pagination';
import { hasActiveDocuments } from '../lib/polling';

export function DocumentsPage() {
  const [isUploadOpen, setIsUploadOpen] = useState(false);
  const { page, status, setPage, setStatus } = useDocumentListParams();
  const query = useDocumentsQuery(toDocumentListParams({ page, status }));

  const documents = query.data?.items ?? [];
  const pageSize = query.data?.pageSize ?? DOCUMENTS_PAGE_SIZE;
  const pageCount = getPageCount(query.data?.total ?? 0, pageSize);

  useEffect(() => {
    if (!query.data || query.isPlaceholderData || page <= pageCount) {
      return;
    }

    setPage(pageCount);
  }, [page, pageCount, query.data, query.isPlaceholderData, setPage]);

  const showError =
    query.isError && (query.isPlaceholderData || !query.data);

  return (
    <div className="mx-auto flex w-full max-w-6xl flex-col gap-6 px-4 py-8 sm:px-6 lg:px-8 lg:py-10">
      <DocumentsHeader
        total={query.data?.total ?? 0}
        hasActiveDocuments={hasActiveDocuments(documents)}
        onUploadClick={() => setIsUploadOpen(true)}
      />

      <div className="flex flex-col gap-4">
        <DocumentStatusFilter value={status} onChange={setStatus} />

        {query.isPending ? <DocumentsSkeleton /> : null}

        {!query.isPending && showError ? (
          <DocumentsErrorState
            error={query.error}
            onRetry={() => {
              void query.refetch();
            }}
          />
        ) : null}

        {!query.isPending && !showError && query.data ? (
          query.data.items.length === 0 ? (
            <DocumentsEmptyState
              status={status}
              onClearFilter={() => setStatus(undefined)}
            />
          ) : (
            <>
              <DocumentsList
                documents={query.data.items}
                isUpdating={query.isPlaceholderData}
              />
              <DocumentsPagination
                page={page}
                pageSize={pageSize}
                total={query.data.total}
                onPageChange={setPage}
              />
            </>
          )
        ) : null}
      </div>

      <UploadDialog open={isUploadOpen} onOpenChange={setIsUploadOpen} />
    </div>
  );
}
