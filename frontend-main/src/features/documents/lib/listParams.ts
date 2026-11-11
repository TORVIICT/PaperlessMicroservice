import {
  DOCUMENT_STATUSES,
  type DocumentListParams,
  type DocumentStatus,
} from '../models/document';

export const DOCUMENTS_PAGE_SIZE = 20;

export interface DocumentListUrlState {
  page: number;
  status?: DocumentStatus;
}

export function isDocumentStatus(value: string): value is DocumentStatus {
  return (DOCUMENT_STATUSES as readonly string[]).includes(value);
}

export function parseDocumentListState(
  searchParams: URLSearchParams,
): DocumentListUrlState {
  const rawPage = Number(searchParams.get('page'));
  const rawStatus = searchParams.get('status');

  return {
    page: Number.isInteger(rawPage) && rawPage > 0 ? rawPage : 1,
    ...(rawStatus && isDocumentStatus(rawStatus)
      ? { status: rawStatus }
      : {}),
  };
}

export function buildDocumentListSearchParams({
  page,
  status,
}: DocumentListUrlState): URLSearchParams {
  const searchParams = new URLSearchParams();

  if (status) {
    searchParams.set('status', status);
  }

  if (Number.isInteger(page) && page > 1) {
    searchParams.set('page', String(page));
  }

  return searchParams;
}

export function toDocumentListParams({
  page,
  status,
}: DocumentListUrlState): DocumentListParams {
  return {
    page,
    pageSize: DOCUMENTS_PAGE_SIZE,
    ...(status ? { status } : {}),
  };
}
