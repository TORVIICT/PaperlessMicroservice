import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { documentsApi } from '@/features/documents/api/documentsApi';
import { UploadDialog } from '@/features/documents/components/upload/UploadDialog';
import type { OcrDocument } from '@/features/documents/models/document';
import { MAX_FILE_SIZE_BYTES } from '@/features/documents/models/documentConstraints';
import { ApiError } from '@/shared/api/ApiError';
import { createQueryClientWrapper, createTestQueryClient } from '../../../utils/queryClient';

vi.mock('@/features/documents/api/documentsApi', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/features/documents/api/documentsApi')>();
  return { documentsApi: { ...actual.documentsApi, upload: vi.fn() } };
});

const uploadMock = vi.mocked(documentsApi.upload);

function createUploadedDocument(file: File): OcrDocument {
  return {
    id: crypto.randomUUID(),
    fileName: file.name,
    contentType: file.type,
    sizeBytes: file.size,
    status: 'queued',
    createdAt: '2026-01-01T12:00:00.000Z',
    completedAt: null,
    pageCount: null,
    error: null,
  };
}

function renderDialog() {
  const onOpenChange = vi.fn();
  render(<UploadDialog open onOpenChange={onOpenChange} />, {
    wrapper: createQueryClientWrapper(createTestQueryClient()),
  });
  return { onOpenChange };
}

beforeEach(() => uploadMock.mockReset());

describe('UploadDialog', () => {
  it('uploads several files independently and shows each result', async () => {
    const user = userEvent.setup();
    const files = [
      new File(['pdf'], 'contrato.pdf', { type: 'application/pdf' }),
      new File(['png'], 'escaneo.png', { type: 'image/png' }),
      new File(['jpg'], 'foto.jpg', { type: 'image/jpeg' }),
    ];
    uploadMock.mockResolvedValue(createUploadedDocument(files[0]));
    renderDialog();

    await user.upload(screen.getByLabelText('Seleccionar documentos'), files);
    expect(screen.getByRole('button', { name: 'Subir 3 documentos' })).toBeEnabled();
    await user.click(screen.getByRole('button', { name: 'Subir 3 documentos' }));

    expect(await screen.findByRole('heading', { name: 'Carga finalizada' })).toBeInTheDocument();
    expect(screen.getByText('contrato.pdf')).toBeInTheDocument();
    expect(screen.getByText('escaneo.png')).toBeInTheDocument();
    expect(screen.getByText('foto.jpg')).toBeInTheDocument();
    expect(screen.getAllByText('Enviado al OCR')).toHaveLength(3);
    expect(uploadMock).toHaveBeenCalledTimes(3);
  });

  it('limits simultaneous uploads to three files', async () => {
    const user = userEvent.setup();
    const files = Array.from(
      { length: 5 },
      (_, index) =>
        new File([String(index)], `documento-${index + 1}.pdf`, {
          type: 'application/pdf',
        }),
    );
    let activeUploads = 0;
    let maximumActiveUploads = 0;

    uploadMock.mockImplementation(async () => {
      activeUploads += 1;
      maximumActiveUploads = Math.max(maximumActiveUploads, activeUploads);
      await new Promise((resolve) => setTimeout(resolve, 10));
      activeUploads -= 1;
      return createUploadedDocument(files[0]);
    });
    renderDialog();

    await user.upload(screen.getByLabelText('Seleccionar documentos'), files);
    await user.click(screen.getByRole('button', { name: 'Subir 5 documentos' }));

    expect(await screen.findByRole('heading', { name: 'Carga finalizada' })).toBeInTheDocument();
    expect(maximumActiveUploads).toBe(3);
    expect(uploadMock).toHaveBeenCalledTimes(5);
    expect(
      uploadMock.mock.calls.map(([file]) => file.name),
    ).toEqual(files.map((file) => file.name));
  });

  it('rejects an unsupported file without calling the API', async () => {
    const user = userEvent.setup({ applyAccept: false });
    renderDialog();
    await user.upload(
      screen.getByLabelText('Seleccionar documentos'),
      new File(['x'], 'script.exe', { type: 'application/x-msdownload' }),
    );
    expect(await screen.findByText('UNSUPPORTED_FILE_TYPE')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Subir documento' })).toBeDisabled();
    expect(uploadMock).not.toHaveBeenCalled();
  });

  it('rejects a file above the size limit', async () => {
    const user = userEvent.setup();
    const file = new File(['x'], 'grande.pdf', { type: 'application/pdf' });
    Object.defineProperty(file, 'size', { value: MAX_FILE_SIZE_BYTES + 1 });
    renderDialog();
    await user.upload(screen.getByLabelText('Seleccionar documentos'), file);
    expect(await screen.findByText('FILE_TOO_LARGE')).toBeInTheDocument();
    expect(uploadMock).not.toHaveBeenCalled();
  });

  it('shows individual failures and retries only failed files', async () => {
    const user = userEvent.setup();
    const good = new File(['ok'], 'correcto.pdf', { type: 'application/pdf' });
    const bad = new File(['bad'], 'fallido.pdf', { type: 'application/pdf' });
    uploadMock
      .mockImplementationOnce(async () => createUploadedDocument(good))
      .mockRejectedValueOnce(new ApiError(503, { code: 'SERVICE_UNAVAILABLE', message: 'No disponible', documentId: null, details: {} }))
      .mockImplementationOnce(async () => createUploadedDocument(bad));
    renderDialog();

    await user.upload(screen.getByLabelText('Seleccionar documentos'), [good, bad]);
    await user.click(screen.getByRole('button', { name: 'Subir 2 documentos' }));
    expect(
      await screen.findByText(/El servicio OCR no está disponible/),
    ).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Reintentar fallidos' }));
    await waitFor(() => expect(uploadMock).toHaveBeenCalledTimes(3));
    await waitFor(() =>
      expect(
        screen.queryByRole('button', { name: 'Reintentar fallidos' }),
      ).not.toBeInTheDocument(),
    );
  });

  it('allows removing one selected file', async () => {
    const user = userEvent.setup();
    const files = [
      new File(['a'], 'uno.pdf', { type: 'application/pdf' }),
      new File(['b'], 'dos.pdf', { type: 'application/pdf' }),
    ];
    renderDialog();
    await user.upload(screen.getByLabelText('Seleccionar documentos'), files);
    await user.click(screen.getByRole('button', { name: 'Quitar uno.pdf' }));
    expect(screen.queryByText('uno.pdf')).not.toBeInTheDocument();
    expect(screen.getByText('dos.pdf')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Subir documento' })).toBeEnabled();
  });

  it('does not close while uploads are in progress', async () => {
    const user = userEvent.setup();
    const file = new File(['a'], 'contrato.pdf', { type: 'application/pdf' });
    let resolveUpload: (document: OcrDocument) => void = () => undefined;
    uploadMock.mockReturnValue(new Promise((resolve) => { resolveUpload = resolve; }));
    const { onOpenChange } = renderDialog();
    await user.upload(screen.getByLabelText('Seleccionar documentos'), file);
    await user.click(screen.getByRole('button', { name: 'Subir documento' }));

    expect(screen.getByRole('heading', { name: 'Subiendo documentos' })).toBeInTheDocument();
    expect(within(screen.getByRole('dialog')).getByRole('button', { name: 'Cerrar' })).toBeDisabled();
    await user.keyboard('{Escape}');
    expect(onOpenChange).not.toHaveBeenCalled();

    await act(async () => resolveUpload(createUploadedDocument(file)));
    expect(await screen.findByRole('heading', { name: 'Carga finalizada' })).toBeInTheDocument();
  });
});
