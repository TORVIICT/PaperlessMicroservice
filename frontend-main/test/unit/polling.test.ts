import { describe, expect, it } from 'vitest';

import {
  DOCUMENT_POLLING_INTERVAL_MS,
  hasActiveDocuments,
  resolveDocumentRefetchInterval,
  resolveDocumentsRefetchInterval,
} from '@/features/documents/lib/polling';
import type { OcrDocument } from '@/features/documents/models/document';

function createDocument(status: OcrDocument['status']): OcrDocument {
  return {
    id: `document-${status}`,
    fileName: `${status}.pdf`,
    contentType: 'application/pdf',
    sizeBytes: 1_024,
    status,
    createdAt: '2026-01-01T00:00:00.000Z',
    completedAt: null,
    pageCount: null,
    error: null,
  };
}

describe('polling', () => {
  it('uses the three second interval required by the spec', () => {
    expect(DOCUMENT_POLLING_INTERVAL_MS).toBe(3_000);
  });

  it('detects active documents in a list', () => {
    expect(hasActiveDocuments([])).toBe(false);
    expect(hasActiveDocuments([createDocument('completed')])).toBe(false);
    expect(hasActiveDocuments([createDocument('queued')])).toBe(true);
    expect(
      hasActiveDocuments([
        createDocument('failed'),
        createDocument('processing'),
      ]),
    ).toBe(true);
  });

  it('polls the list only while it contains active documents', () => {
    expect(resolveDocumentsRefetchInterval(undefined)).toBe(false);
    expect(
      resolveDocumentsRefetchInterval([createDocument('completed')]),
    ).toBe(false);
    expect(
      resolveDocumentsRefetchInterval([createDocument('queued')]),
    ).toBe(DOCUMENT_POLLING_INTERVAL_MS);
  });

  it('polls the detail only while the document is active', () => {
    expect(resolveDocumentRefetchInterval(undefined)).toBe(false);
    expect(resolveDocumentRefetchInterval('completed')).toBe(false);
    expect(resolveDocumentRefetchInterval('failed')).toBe(false);
    expect(resolveDocumentRefetchInterval('queued')).toBe(
      DOCUMENT_POLLING_INTERVAL_MS,
    );
    expect(resolveDocumentRefetchInterval('processing')).toBe(
      DOCUMENT_POLLING_INTERVAL_MS,
    );
  });
});
