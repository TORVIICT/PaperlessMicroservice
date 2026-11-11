import type { DocumentStatus, OcrDocument } from '../models/document';
import { isActiveDocumentStatus } from '../models/document';

export const DOCUMENT_POLLING_INTERVAL_MS = 3_000;

export function hasActiveDocuments(
  documents: readonly OcrDocument[],
): boolean {
  return documents.some((document) =>
    isActiveDocumentStatus(document.status),
  );
}

export function resolveDocumentsRefetchInterval(
  documents: readonly OcrDocument[] | undefined,
): number | false {
  return documents && hasActiveDocuments(documents)
    ? DOCUMENT_POLLING_INTERVAL_MS
    : false;
}

export function resolveDocumentRefetchInterval(
  status: DocumentStatus | undefined,
): number | false {
  return status && isActiveDocumentStatus(status)
    ? DOCUMENT_POLLING_INTERVAL_MS
    : false;
}
