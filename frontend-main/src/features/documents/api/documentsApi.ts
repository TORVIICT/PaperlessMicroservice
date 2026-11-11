import { httpClient } from '@/shared/api/httpClient';

import type {
  DocumentListParams,
  DocumentPage,
  DocumentTextResult,
  OcrDocument,
} from '../models/document';

function createListQuery(params: DocumentListParams) {
  const query = new URLSearchParams();

  query.set('page', String(params.page ?? 1));
  query.set('pageSize', String(params.pageSize ?? 20));

  if (params.status) {
    query.set('status', params.status);
  }

  return query.toString();
}

export const documentsApi = Object.freeze({
  upload(file: File, signal?: AbortSignal) {
    const body = new FormData();
    body.append('file', file);

    return httpClient.json<OcrDocument>('documents', {
      method: 'POST',
      body,
      signal,
    });
  },

  list(params: DocumentListParams = {}, signal?: AbortSignal) {
    return httpClient.json<DocumentPage>(
      `documents?${createListQuery(params)}`,
      { signal },
    );
  },

  getById(documentId: string, signal?: AbortSignal) {
    return httpClient.json<OcrDocument>(`documents/${documentId}`, { signal });
  },

  getText(documentId: string, signal?: AbortSignal) {
    return httpClient.json<DocumentTextResult>(
      `documents/${documentId}/text`,
      { signal },
    );
  },

  downloadOriginal(documentId: string, signal?: AbortSignal) {
    return httpClient.blob(`documents/${documentId}/original`, { signal });
  },

  downloadArchive(documentId: string, signal?: AbortSignal) {
    return httpClient.blob(`documents/${documentId}/archive`, { signal });
  },

  async downloadText(documentId: string, signal?: AbortSignal) {
    const result = await httpClient.json<DocumentTextResult>(
      `documents/${documentId}/text`,
      { signal },
    );
    return new Blob([result.text], { type: 'text/plain;charset=utf-8' });
  },

  remove(documentId: string, signal?: AbortSignal) {
    return httpClient.empty(`documents/${documentId}`, {
      method: 'DELETE',
      signal,
    });
  },
});
