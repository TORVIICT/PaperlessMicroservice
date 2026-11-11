export const DOCUMENT_STATUSES = [
  'queued',
  'processing',
  'completed',
  'failed',
] as const;

export type DocumentStatus = (typeof DOCUMENT_STATUSES)[number];

export const ACTIVE_DOCUMENT_STATUSES: readonly DocumentStatus[] = [
  'queued',
  'processing',
];

export function isActiveDocumentStatus(status: DocumentStatus): boolean {
  return ACTIVE_DOCUMENT_STATUSES.includes(status);
}

export interface DocumentProcessingError {
  code: string;
  message: string;
}

export interface OcrDocument {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  status: DocumentStatus;
  createdAt: string;
  completedAt: string | null;
  pageCount: number | null;
  error: DocumentProcessingError | null;
}

export interface DocumentTextResult {
  documentId: string;
  text: string;
  pageCount: number | null;
}

export interface DocumentPage {
  items: OcrDocument[];
  page: number;
  pageSize: number;
  total: number;
}

export interface DocumentListParams {
  page?: number;
  pageSize?: number;
  status?: DocumentStatus;
}
