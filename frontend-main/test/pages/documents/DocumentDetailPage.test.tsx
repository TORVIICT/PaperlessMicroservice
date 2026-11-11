import {
  render,
  screen,
  waitFor,
  within,
} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { saveBlob } from '@/features/documents/lib/saveBlob';
import type { OcrDocument } from '@/features/documents/models/document';
import { DocumentDetailPage } from '@/features/documents/pages/DocumentDetailPage';
import { documentsStore } from '@/mocks/data/documentsStore';
import { env } from '@/shared/config/env';
import { server } from '../../msw/server';
import {
  createAppWrapper,
  createTestQueryClient,
} from '../../utils/queryClient';

vi.mock('@/features/documents/lib/saveBlob', () => ({
  saveBlob: vi.fn(),
}));

const API_URL = env.apiBaseUrl.replace(/\/$/, '');
const COMPLETED_ID = '11111111-1111-4111-8111-111111111111';
const FAILED_ID = '22222222-2222-4222-8222-222222222222';
const ACTIVE_ID = 'active-document';
const COMPLETED_TEXT =
  'Factura de ejemplo procesada correctamente por el OCR.';

const ACTIVE_DOCUMENT: OcrDocument = {
  id: ACTIVE_ID,
  fileName: 'en-proceso.pdf',
  contentType: 'application/pdf',
  sizeBytes: 2048,
  status: 'queued',
  createdAt: '2026-01-01T12:00:00.000Z',
  completedAt: null,
  pageCount: null,
  error: null,
};

function renderDetail(documentId = COMPLETED_ID) {
  return render(
    <Routes>
      <Route
        path="/documents/:documentId"
        element={<DocumentDetailPage />}
      />
      <Route
        path="/documents"
        element={<h1>Listado de documentos</h1>}
      />
    </Routes>,
    {
      wrapper: createAppWrapper(createTestQueryClient(), [
        `/documents/${documentId}`,
      ]),
    },
  );
}

beforeEach(() => {
  vi.mocked(saveBlob).mockClear();
});

describe('DocumentDetailPage', () => {
  it('shows metadata, extracted text and enabled download actions', async () => {
    renderDetail();

    expect(
      await screen.findByRole('heading', {
        name: 'factura-ejemplo.pdf',
      }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole('link', { name: /Volver al listado/ }),
    ).toHaveAttribute('href', '/documents');
    expect(screen.getByText('Completado')).toBeInTheDocument();
    expect(screen.getByText('PDF')).toBeInTheDocument();
    expect(screen.getByText('178,1 kB')).toBeInTheDocument();
    expect(screen.getByText('2 páginas')).toBeInTheDocument();

    expect(await screen.findByText(COMPLETED_TEXT)).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: 'Descargar original' }),
    ).toBeEnabled();
    expect(
      screen.getByRole('button', { name: 'Descargar TXT' }),
    ).toBeEnabled();
  });

  it('downloads the original and the extracted text', async () => {
    const user = userEvent.setup();

    renderDetail();
    await screen.findByRole('heading', {
      name: 'factura-ejemplo.pdf',
    });

    await user.click(
      screen.getByRole('button', { name: 'Descargar original' }),
    );

    await waitFor(() => {
      expect(saveBlob).toHaveBeenCalledWith(
        expect.any(Blob),
        'factura-ejemplo.pdf',
      );
    });

    await user.click(
      screen.getByRole('button', { name: 'Descargar TXT' }),
    );

    await waitFor(() => {
      expect(saveBlob).toHaveBeenCalledWith(
        expect.any(Blob),
        'factura-ejemplo.txt',
      );
    });
  });

  it('shows the processing error and keeps the text download disabled', async () => {
    renderDetail(FAILED_ID);

    expect(
      await screen.findByRole('heading', {
        name: 'escaneo-ilegible.png',
      }),
    ).toBeInTheDocument();
    expect(screen.getByText('Fallido')).toBeInTheDocument();
    expect(
      screen.getByText('El procesamiento OCR falló'),
    ).toBeInTheDocument();
    expect(screen.getByText('OCR_PROCESSING_FAILED')).toBeInTheDocument();
    expect(
      screen.getByText('El OCR no pudo extraer texto del documento.'),
    ).toBeInTheDocument();
    expect(screen.getByText('No hay texto disponible')).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: 'Descargar TXT' }),
    ).toBeDisabled();
    expect(
      screen.getByRole('button', { name: 'Descargar original' }),
    ).toBeEnabled();
  });

  it('shows the active processing state and automatic updates', async () => {
    server.use(
      http.get(`${API_URL}/documents/${ACTIVE_ID}`, () =>
        HttpResponse.json(ACTIVE_DOCUMENT),
      ),
    );

    renderDetail(ACTIVE_ID);

    expect(
      await screen.findByRole('heading', { name: 'en-proceso.pdf' }),
    ).toBeInTheDocument();
    expect(
      screen.getByTitle(
        'El documento fue recibido y espera ser procesado.',
      ),
    ).toHaveTextContent('En cola');
    expect(
      screen.getByText('Actualización automática activa'),
    ).toBeInTheDocument();
    expect(
      screen.getByText(/El texto aparecerá automáticamente/),
    ).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: 'Descargar TXT' }),
    ).toBeDisabled();
  });

  it('shows a not found state when the document does not exist', async () => {
    renderDetail('missing-document');

    const alert = await screen.findByRole('alert');

    expect(
      within(alert).getByRole('heading', {
        name: 'Documento no encontrado',
      }),
    ).toBeInTheDocument();
    expect(alert).toHaveTextContent('DOCUMENT_NOT_FOUND');
    expect(
      within(alert).queryByRole('button', { name: 'Reintentar' }),
    ).not.toBeInTheDocument();
    expect(
      screen.getByRole('link', { name: /Volver al listado/ }),
    ).toHaveAttribute('href', '/documents');
  });

  it('retries the document request after a server error', async () => {
    let attempts = 0;

    server.use(
      http.get(`${API_URL}/documents/${COMPLETED_ID}`, () => {
        attempts += 1;

        if (attempts === 1) {
          return HttpResponse.json(
            {
              code: 'SERVICE_UNAVAILABLE',
              message: 'El servicio no está disponible.',
              documentId: null,
              details: {},
            },
            { status: 503 },
          );
        }

        const document = documentsStore.find(COMPLETED_ID);

        return document
          ? HttpResponse.json(document)
          : new HttpResponse(null, { status: 404 });
      }),
    );

    const user = userEvent.setup();

    renderDetail();

    const alert = await screen.findByRole('alert');

    expect(alert).toHaveTextContent('No pudimos cargar el documento');
    expect(alert).toHaveTextContent('SERVICE_UNAVAILABLE');

    await user.click(
      within(alert).getByRole('button', { name: 'Reintentar' }),
    );

    expect(
      await screen.findByRole('heading', {
        name: 'factura-ejemplo.pdf',
      }),
    ).toBeInTheDocument();
  });

  it('shows an error when the extracted text cannot be loaded', async () => {
    server.use(
      http.get(
        `${API_URL}/documents/${COMPLETED_ID}/text`,
        () => new HttpResponse(null, { status: 503 }),
      ),
    );

    renderDetail();

    await screen.findByRole('heading', {
      name: 'factura-ejemplo.pdf',
    });

    const alert = await screen.findByRole('alert');

    expect(alert).toHaveTextContent('No pudimos cargar el texto');
    expect(alert).toHaveTextContent('SERVICE_UNAVAILABLE');
  });

  it('shows an error when a download fails', async () => {
    server.use(
      http.get(
        `${API_URL}/documents/${COMPLETED_ID}/original`,
        () => new HttpResponse(null, { status: 503 }),
      ),
    );

    const user = userEvent.setup();

    renderDetail();
    await screen.findByRole('heading', {
      name: 'factura-ejemplo.pdf',
    });

    await user.click(
      screen.getByRole('button', { name: 'Descargar original' }),
    );

    const alert = await screen.findByRole('alert');

    expect(alert).toHaveTextContent('No pudimos descargar el archivo');
    expect(alert).toHaveTextContent('SERVICE_UNAVAILABLE');
    expect(
      within(alert).getByRole('button', { name: 'Reintentar' }),
    ).toBeInTheDocument();
    expect(saveBlob).not.toHaveBeenCalled();
  });

  it('confirms deletion and returns to the list', async () => {
    const user = userEvent.setup();
    const document = documentsStore.create(
      new File(['contenido'], 'eliminar.pdf', {
        type: 'application/pdf',
      }),
    );

    renderDetail(document.id);

    await screen.findByRole('heading', { name: 'eliminar.pdf' });

    await user.click(
      screen.getByRole('button', { name: 'Eliminar documento' }),
    );

    const dialog = screen.getByRole('dialog');

    expect(
      within(dialog).getByRole('heading', {
        name: 'Eliminar documento',
      }),
    ).toBeInTheDocument();

    await user.click(
      within(dialog).getByRole('button', { name: 'Cancelar' }),
    );

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(documentsStore.find(document.id)).toBeDefined();

    await user.click(
      screen.getByRole('button', { name: 'Eliminar documento' }),
    );

    await user.click(
      within(screen.getByRole('dialog')).getByRole('button', {
        name: 'Eliminar',
      }),
    );

    expect(
      await screen.findByRole('heading', {
        name: 'Listado de documentos',
      }),
    ).toBeInTheDocument();
    expect(documentsStore.find(document.id)).toBeUndefined();
  });

  it('keeps the delete dialog open when deletion fails', async () => {
    server.use(
      http.delete(`${API_URL}/documents/:documentId`, () =>
        HttpResponse.json(
          {
            code: 'SERVICE_UNAVAILABLE',
            message: 'El servicio no está disponible.',
            documentId: null,
            details: {},
          },
          { status: 503 },
        ),
      ),
    );

    const user = userEvent.setup();
    const document = documentsStore.create(
      new File(['contenido'], 'no-eliminar.pdf', {
        type: 'application/pdf',
      }),
    );

    renderDetail(document.id);

    await screen.findByRole('heading', { name: 'no-eliminar.pdf' });

    await user.click(
      screen.getByRole('button', { name: 'Eliminar documento' }),
    );
    await user.click(
      within(screen.getByRole('dialog')).getByRole('button', {
        name: 'Eliminar',
      }),
    );

    expect(
      await screen.findByText('No pudimos eliminar el documento'),
    ).toBeInTheDocument();
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(
      within(screen.getByRole('dialog')).getByRole('button', {
        name: 'Reintentar',
      }),
    ).toBeInTheDocument();
    expect(documentsStore.find(document.id)).toBeDefined();

    documentsStore.remove(document.id);
  });
});
