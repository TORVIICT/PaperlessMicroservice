// @vitest-environment node

import { describe, expect, it } from 'vitest';

import { documentsApi } from '@/features/documents/api/documentsApi';

const FAILED_ID = '22222222-2222-4222-8222-222222222222';

function createPdf(name: string) {
  return new File(['contenido'], name, { type: 'application/pdf' });
}

describe('documentsApi', () => {
  it('uploads a supported file as multipart form data', async () => {
    const document = await documentsApi.upload(createPdf('contrato.pdf'));

    expect(document).toMatchObject({
      fileName: 'contrato.pdf',
      contentType: 'application/pdf',
      status: 'queued',
      completedAt: null,
      pageCount: null,
      error: null,
    });
    expect(document.id).toEqual(expect.any(String));
  });

  it('rejects empty files with INVALID_FILE', async () => {
    const file = new File([], 'vacio.pdf', { type: 'application/pdf' });

    await expect(documentsApi.upload(file)).rejects.toMatchObject({
      status: 400,
      code: 'INVALID_FILE',
    });
  });

  it('rejects unsupported content types', async () => {
    const file = new File(['texto'], 'nota.txt', { type: 'text/plain' });

    await expect(documentsApi.upload(file)).rejects.toMatchObject({
      status: 415,
      code: 'UNSUPPORTED_FILE_TYPE',
    });
  });

  it('reports DOCUMENT_NOT_READY while the document is queued', async () => {
    const document = await documentsApi.upload(createPdf('pendiente.pdf'));

    await expect(documentsApi.getText(document.id)).rejects.toMatchObject({
      status: 409,
      code: 'DOCUMENT_NOT_READY',
    });
  });

  it('reports OCR_PROCESSING_FAILED for failed documents', async () => {
    await expect(documentsApi.getText(FAILED_ID)).rejects.toMatchObject({
      status: 409,
      code: 'OCR_PROCESSING_FAILED',
    });
  });
});
