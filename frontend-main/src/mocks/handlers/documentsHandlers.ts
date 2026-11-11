import { env } from '@/shared/config/env';
import type { ApiErrorPayload } from '@/shared/api/ApiError';
import type { DocumentStatus } from '@/features/documents/models/document';
import {
  isSupportedContentType,
  MAX_FILE_SIZE_BYTES,
  MAX_FILE_SIZE_MB,
} from '@/features/documents/models/documentConstraints';
import { delay, http, HttpResponse } from 'msw';

import { documentsStore } from '../data/documentsStore';

const API_URL = env.apiBaseUrl.replace(/\/$/, '');

function errorResponse(
  status: number,
  code: string,
  message: string,
  documentId: string | null = null,
) {
  return HttpResponse.json<ApiErrorPayload>(
    { code, message, documentId, details: {} },
    { status },
  );
}

function positiveInteger(value: string | null, fallback: number) {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : fallback;
}

export const documentsHandlers = [
  http.post(`${API_URL}/documents`, async ({ request }) => {
    await delay(400);
    const formData = await request.formData();
    const file = formData.get('file');

    if (!(file instanceof File) || file.size === 0) {
      return errorResponse(400, 'INVALID_FILE', 'Debe seleccionar un archivo válido.');
    }

    if (!isSupportedContentType(file.type)) {
      return errorResponse(
        415,
        'UNSUPPORTED_FILE_TYPE',
        'El formato del archivo no está soportado.',
      );
    }

    if (file.size > MAX_FILE_SIZE_BYTES) {
      return errorResponse(
        413,
        'FILE_TOO_LARGE',
        `El archivo supera el límite permitido de ${MAX_FILE_SIZE_MB} MB.`,
      );
    }

    return HttpResponse.json(documentsStore.create(file), { status: 202 });
  }),

  http.get(`${API_URL}/documents`, async ({ request }) => {
    await delay(250);
    const url = new URL(request.url);
    const page = positiveInteger(url.searchParams.get('page'), 1);
    const pageSize = positiveInteger(url.searchParams.get('pageSize'), 20);
    const status = url.searchParams.get('status') as DocumentStatus | null;

    return HttpResponse.json(
      documentsStore.list(page, Math.min(pageSize, 100), status ?? undefined),
    );
  }),

  http.get(`${API_URL}/documents/:documentId`, async ({ params }) => {
    await delay(200);
    const documentId = String(params.documentId);
    const document = documentsStore.find(documentId);

    return document
      ? HttpResponse.json(document)
      : errorResponse(404, 'DOCUMENT_NOT_FOUND', 'El documento no existe.', documentId);
  }),

  http.get(`${API_URL}/documents/:documentId/text`, async ({ params }) => {
    await delay(200);
    const documentId = String(params.documentId);
    const document = documentsStore.find(documentId);

    if (!document) {
      return errorResponse(404, 'DOCUMENT_NOT_FOUND', 'El documento no existe.', documentId);
    }

    if (document.status === 'failed') {
      return errorResponse(
        409,
        'OCR_PROCESSING_FAILED',
        document.error?.message ?? 'El procesamiento del documento falló.',
        documentId,
      );
    }

    const result = documentsStore.getText(documentId);
    return result
      ? HttpResponse.json(result)
      : errorResponse(
          409,
          'DOCUMENT_NOT_READY',
          'El procesamiento del documento todavía no ha finalizado.',
          documentId,
        );
  }),

  http.get(`${API_URL}/documents/:documentId/original`, ({ params }) => {
    const documentId = String(params.documentId);
    const original = documentsStore.getOriginal(documentId);

    return original
      ? new HttpResponse(original)
      : errorResponse(404, 'DOCUMENT_NOT_FOUND', 'El documento no existe.', documentId);
  }),

  http.get(`${API_URL}/documents/:documentId/archive`, ({ params }) => {
    const documentId = String(params.documentId);
    const document = documentsStore.find(documentId);

    if (!document) {
      return errorResponse(404, 'DOCUMENT_NOT_FOUND', 'El documento no existe.', documentId);
    }

    if (document.status === 'failed') {
      return errorResponse(
        409,
        'OCR_PROCESSING_FAILED',
        document.error?.message ?? 'El procesamiento del documento falló.',
        documentId,
      );
    }

    if (document.status !== 'completed') {
      return errorResponse(
        409,
        'DOCUMENT_NOT_READY',
        'El PDF buscable todavía no está disponible.',
        documentId,
      );
    }

    return new HttpResponse(new Blob(['PDF buscable simulado'], { type: 'application/pdf' }));
  }),

  http.delete(`${API_URL}/documents/:documentId`, ({ params }) => {
    const documentId = String(params.documentId);

    return documentsStore.remove(documentId)
      ? new HttpResponse(null, { status: 204 })
      : errorResponse(404, 'DOCUMENT_NOT_FOUND', 'El documento no existe.', documentId);
  }),
];
