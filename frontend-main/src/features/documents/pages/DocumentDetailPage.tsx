import { useState } from 'react';
import { ArrowLeft, RefreshCw } from 'lucide-react';
import { Link, useNavigate, useParams } from 'react-router-dom';

import { Button } from '@/components/ui/button';
import { ROUTES } from '@/shared/routes';

import { ErrorAlert } from '../components/ErrorAlert';
import { DeleteDocumentDialog } from '../components/detail/DeleteDocumentDialog';
import { DocumentActions } from '../components/detail/DocumentActions';
import { DocumentDetailErrorState } from '../components/detail/DocumentDetailErrorState';
import { DocumentDetailHeader } from '../components/detail/DocumentDetailHeader';
import { DocumentDetailSkeleton } from '../components/detail/DocumentDetailSkeleton';
import { DocumentMetadata } from '../components/detail/DocumentMetadata';
import { DocumentTextSection } from '../components/detail/DocumentTextSection';
import { useDeleteDocumentMutation } from '../hooks/useDeleteDocumentMutation';
import { useDocumentDownloadMutation } from '../hooks/useDocumentDownloadMutation';
import { useDocumentQuery } from '../hooks/useDocumentQuery';
import { useDocumentTextQuery } from '../hooks/useDocumentTextQuery';
import { isDocumentReadyForResults } from '../lib/documentAvailability';
import type { DocumentDownloadVariant } from '../lib/downloadFileName';
import {
  getUserFacingMessage,
  toUserFacingError,
} from '../lib/errorMessages';

export function DocumentDetailPage() {
  const { documentId = '' } = useParams<{ documentId: string }>();
  const navigate = useNavigate();
  const [isDeleteOpen, setIsDeleteOpen] = useState(false);

  const query = useDocumentQuery(documentId);
  const document = query.data;
  const textQuery = useDocumentTextQuery(documentId, {
    enabled: document
      ? isDocumentReadyForResults(document.status)
      : false,
  });
  const downloadMutation = useDocumentDownloadMutation();
  const deleteMutation = useDeleteDocumentMutation();

  const showError = query.isError && !document;
  const downloadingVariant = downloadMutation.isPending
    ? (downloadMutation.variables?.variant ?? null)
    : null;
  const downloadError = downloadMutation.isError
    ? toUserFacingError(downloadMutation.error)
    : null;
  const processingError =
    document?.status === 'failed'
      ? {
          code: document.error?.code ?? 'OCR_PROCESSING_FAILED',
          message:
            document.error?.message ??
            getUserFacingMessage('OCR_PROCESSING_FAILED'),
        }
      : null;

  function handleDownload(variant: DocumentDownloadVariant) {
    if (!document) {
      return;
    }

    downloadMutation.mutate({ document, variant });
  }

  function handleRetryDownload() {
    const variant = downloadMutation.variables?.variant;

    if (variant) {
      handleDownload(variant);
    }
  }

  function handleDeleteDialogOpenChange(open: boolean) {
    if (!open && deleteMutation.isPending) {
      return;
    }

    if (!open) {
      deleteMutation.reset();
    }

    setIsDeleteOpen(open);
  }

  function handleDeleteRequest() {
    deleteMutation.reset();
    setIsDeleteOpen(true);
  }

  function handleDeleteConfirm() {
    if (!document) {
      return;
    }

    deleteMutation.mutate(document.id, {
      onSuccess: () => {
        setIsDeleteOpen(false);
        navigate(ROUTES.documents);
      },
    });
  }

  return (
    <div className="mx-auto flex w-full max-w-5xl flex-col gap-5 px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
      <Button asChild variant="ghost" size="sm" className="-ml-2 w-fit text-muted-foreground">
        <Link to={ROUTES.documents}>
          <ArrowLeft aria-hidden="true" />
          Volver al listado
        </Link>
      </Button>

      {query.isPending ? <DocumentDetailSkeleton /> : null}

      {!query.isPending && showError ? (
        <DocumentDetailErrorState
          error={query.error}
          onRetry={() => {
            void query.refetch();
          }}
        />
      ) : null}

      {!query.isPending && document ? (
        <>
          <DocumentDetailHeader document={document} />

          <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_18rem]">
            <div className="space-y-6">
              <DocumentMetadata document={document} />

              {processingError ? (
                <ErrorAlert
                  title="El procesamiento OCR falló"
                  code={processingError.code}
                  message={processingError.message}
                />
              ) : null}

              <DocumentTextSection
                status={document.status}
                text={textQuery.data}
                isLoading={textQuery.isPending}
                error={textQuery.error}
                onRetry={() => {
                  void textQuery.refetch();
                }}
              />
            </div>

            <aside className="space-y-4 lg:sticky lg:top-6 lg:self-start">
              <DocumentActions
                document={document}
                downloadingVariant={downloadingVariant}
                onDownload={handleDownload}
                onDelete={handleDeleteRequest}
              />

              {downloadError ? (
                <ErrorAlert
                  title="No pudimos descargar el archivo"
                  code={downloadError.code}
                  message={downloadError.message}
                  action={
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      onClick={handleRetryDownload}
                    >
                      <RefreshCw aria-hidden="true" />
                      Reintentar
                    </Button>
                  }
                />
              ) : null}
            </aside>
          </div>

          <DeleteDocumentDialog
            document={document}
            open={isDeleteOpen}
            isDeleting={deleteMutation.isPending}
            error={deleteMutation.error}
            onOpenChange={handleDeleteDialogOpenChange}
            onConfirm={handleDeleteConfirm}
          />
        </>
      ) : null}
    </div>
  );
}
