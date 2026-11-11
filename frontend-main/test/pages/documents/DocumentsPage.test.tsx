import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { useLocation } from 'react-router-dom';
import { describe, expect, it } from 'vitest';

import type { OcrDocument } from '@/features/documents/models/document';
import { DocumentsPage } from '@/features/documents/pages/DocumentsPage';
import { documentsStore } from '@/mocks/data/documentsStore';
import { env } from '@/shared/config/env';
import { server } from '../../msw/server';
import {
  createAppWrapper,
  createTestQueryClient,
} from '../../utils/queryClient';

const API_URL = env.apiBaseUrl.replace(/\/$/, '');
const FIRST_DOCUMENT_ID = '11111111-1111-4111-8111-111111111111';

const PAGINATED_DOCUMENTS: OcrDocument[] = Array.from(
  { length: 25 },
  (_, index) => {
    const documentNumber = index + 1;

    return {
      id: `document-${documentNumber}`,
      fileName: `documento-${documentNumber}.pdf`,
      contentType: 'application/pdf',
      sizeBytes: 1024 * documentNumber,
      status: 'completed',
      createdAt: '2026-01-01T12:00:00.000Z',
      completedAt: '2026-01-01T12:01:00.000Z',
      pageCount: 1,
      error: null,
    };
  },
);

function LocationProbe() {
  const location = useLocation();

  return (
    <output data-testid="location">
      {location.pathname}
      {location.search}
    </output>
  );
}

function renderDocumentsPage(initialEntry = '/documents') {
  return render(
    <>
      <DocumentsPage />
      <LocationProbe />
    </>,
    {
      wrapper: createAppWrapper(createTestQueryClient(), [initialEntry]),
    },
  );
}

describe('DocumentsPage', () => {
  it('shows a loading state and then the document list', async () => {
    renderDocumentsPage();

    expect(
      screen.getByRole('status', { name: 'Cargando documentos' }),
    ).toBeInTheDocument();

    expect(await screen.findByText('factura-ejemplo.pdf')).toBeInTheDocument();
    expect(screen.getByText('escaneo-ilegible.png')).toBeInTheDocument();
    expect(
      screen.getByRole('link', {
        name: /Ver detalle de factura-ejemplo\.pdf/,
      }),
    ).toHaveAttribute('href', `/documents/${FIRST_DOCUMENT_ID}`);
  });

  it('filters by status and keeps the filter in the URL', async () => {
    const user = userEvent.setup();

    renderDocumentsPage();
    await screen.findByText('factura-ejemplo.pdf');

    await user.click(screen.getByRole('button', { name: 'Fallido' }));

    await waitFor(() => {
      expect(
        screen.queryByText('factura-ejemplo.pdf'),
      ).not.toBeInTheDocument();
    });
    expect(await screen.findByText('escaneo-ilegible.png')).toBeInTheDocument();
    expect(screen.getByTestId('location')).toHaveTextContent(
      '/documents?status=failed',
    );
    expect(
      screen.getByRole('button', { name: 'Fallido' }),
    ).toHaveAttribute('aria-pressed', 'true');
  });

  it('shows a filtered empty state and clears the filter', async () => {
    server.use(
      http.get(`${API_URL}/documents`, ({ request }) => {
        const url = new URL(request.url);

        if (url.searchParams.get('status') === 'failed') {
          return HttpResponse.json({
            items: [],
            page: 1,
            pageSize: 20,
            total: 0,
          });
        }

        return HttpResponse.json(documentsStore.list(1, 20));
      }),
    );

    const user = userEvent.setup();

    renderDocumentsPage('/documents?status=failed');

    expect(
      await screen.findByText(/No hay documentos en estado/),
    ).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Limpiar filtro' }));

    expect(await screen.findByText('factura-ejemplo.pdf')).toBeInTheDocument();
    expect(screen.getByTestId('location')).not.toHaveTextContent(
      'status=failed',
    );
  });

  it('shows the error code and retries the request', async () => {
    let shouldFail = true;

    server.use(
      http.get(`${API_URL}/documents`, () => {
        if (shouldFail) {
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

        return HttpResponse.json(documentsStore.list(1, 20));
      }),
    );

    const user = userEvent.setup();

    renderDocumentsPage();

    const alert = await screen.findByRole('alert');

    expect(alert).toHaveTextContent('No pudimos cargar los documentos');
    expect(alert).toHaveTextContent('SERVICE_UNAVAILABLE');

    shouldFail = false;
    await user.click(screen.getByRole('button', { name: 'Reintentar' }));

    expect(await screen.findByText('factura-ejemplo.pdf')).toBeInTheDocument();
  });

  it('opens the upload dialog from the header', async () => {
    const user = userEvent.setup();

    renderDocumentsPage();
    await screen.findByText('factura-ejemplo.pdf');

    await user.click(
      screen.getByRole('button', { name: 'Cargar documento' }),
    );

    expect(await screen.findByRole('dialog')).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { name: 'Cargar documentos' }),
    ).toBeInTheDocument();
  });

  it('paginates and keeps the page in the URL', async () => {
    server.use(
      http.get(`${API_URL}/documents`, ({ request }) => {
        const url = new URL(request.url);
        const page = Number(url.searchParams.get('page') ?? 1);
        const start = (page - 1) * 20;

        return HttpResponse.json({
          items: PAGINATED_DOCUMENTS.slice(start, start + 20),
          page,
          pageSize: 20,
          total: PAGINATED_DOCUMENTS.length,
        });
      }),
    );

    const user = userEvent.setup();

    renderDocumentsPage();

    expect(await screen.findByText('documento-1.pdf')).toBeInTheDocument();
    expect(screen.getByText('1–20 de 25 documentos')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Siguiente' }));

    expect(await screen.findByText('documento-21.pdf')).toBeInTheDocument();
    expect(screen.getByText('21–25 de 25 documentos')).toBeInTheDocument();
    expect(screen.getByTestId('location')).toHaveTextContent(
      '/documents?page=2',
    );
  });
});
