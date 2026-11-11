import type {
  DocumentPage,
  DocumentStatus,
  OcrDocument,
} from '@/features/documents/models/document';

import {
  resolveProcessingState,
  type ProcessingSource,
  type ProcessingState,
} from './processingSimulator';

interface StoredDocument extends ProcessingSource {
  id: string;
  contentType: string;
  sizeBytes: number;
  original: Blob;
}

const FAILURE_FILE_NAME_FRAGMENT = 'fail';

function secondsAgo(seconds: number) {
  return new Date(Date.now() - seconds * 1_000).toISOString();
}

const records = new Map<string, StoredDocument>([
  [
    '11111111-1111-4111-8111-111111111111',
    {
      id: '11111111-1111-4111-8111-111111111111',
      fileName: 'factura-ejemplo.pdf',
      contentType: 'application/pdf',
      sizeBytes: 182_400,
      createdAt: secondsAgo(60),
      outcome: 'completed',
      alreadyProcessed: true,
      completedAt: secondsAgo(50),
      pageCount: 2,
      extractedText: 'Factura de ejemplo procesada correctamente por el OCR.',
      original: new Blob(['Documento de ejemplo'], { type: 'application/pdf' }),
    },
  ],
  [
    '22222222-2222-4222-8222-222222222222',
    {
      id: '22222222-2222-4222-8222-222222222222',
      fileName: 'escaneo-ilegible.png',
      contentType: 'image/png',
      sizeBytes: 421_000,
      createdAt: secondsAgo(120),
      outcome: 'failed',
      alreadyProcessed: true,
      completedAt: secondsAgo(110),
      pageCount: null,
      extractedText: '',
      original: new Blob(['Imagen de ejemplo'], { type: 'image/png' }),
    },
  ],
]);

function toOcrDocument(
  record: StoredDocument,
  state: ProcessingState = resolveProcessingState(record),
): OcrDocument {
  return {
    id: record.id,
    fileName: record.fileName,
    contentType: record.contentType,
    sizeBytes: record.sizeBytes,
    status: state.status,
    createdAt: record.createdAt,
    completedAt: state.completedAt,
    pageCount: state.pageCount,
    error: state.error,
  };
}

export const documentsStore = Object.freeze({
  create(file: File) {
    const record: StoredDocument = {
      id: crypto.randomUUID(),
      fileName: file.name,
      contentType: file.type,
      sizeBytes: file.size,
      createdAt: new Date().toISOString(),
      outcome: file.name.toLowerCase().includes(FAILURE_FILE_NAME_FRAGMENT)
        ? 'failed'
        : 'completed',
      alreadyProcessed: false,
      completedAt: null,
      pageCount: null,
      extractedText: '',
      original: file,
    };

    records.set(record.id, record);
    return toOcrDocument(record);
  },

  find(documentId: string) {
    const record = records.get(documentId);
    return record ? toOcrDocument(record) : undefined;
  },

  getOriginal(documentId: string) {
    return records.get(documentId)?.original;
  },

  getText(documentId: string) {
    const record = records.get(documentId);

    if (!record) {
      return undefined;
    }

    const state = resolveProcessingState(record);

    if (state.status !== 'completed') {
      return undefined;
    }

    return {
      documentId: record.id,
      text: state.extractedText,
      pageCount: state.pageCount,
    };
  },

  list(page: number, pageSize: number, status?: DocumentStatus): DocumentPage {
    const now = Date.now();
    const documents = [...records.values()]
      .map((record) =>
        toOcrDocument(record, resolveProcessingState(record, now)),
      )
      .filter((document) => !status || document.status === status)
      .sort((left, right) => right.createdAt.localeCompare(left.createdAt));

    const start = (page - 1) * pageSize;

    return {
      items: documents.slice(start, start + pageSize),
      page,
      pageSize,
      total: documents.length,
    };
  },

  remove(documentId: string) {
    return records.delete(documentId);
  },
});
