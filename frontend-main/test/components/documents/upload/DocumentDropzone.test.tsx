import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { DocumentDropzone } from '@/features/documents/components/upload/DocumentDropzone';

afterEach(() => {
  vi.restoreAllMocks();
});

describe('DocumentDropzone', () => {
  it('selects a file through the file input', async () => {
    const user = userEvent.setup();
    const onFilesSelected = vi.fn();
    const file = new File(['contenido'], 'factura.pdf', {
      type: 'application/pdf',
    });

    render(<DocumentDropzone onFilesSelected={onFilesSelected} />);

    await user.upload(
      screen.getByLabelText('Seleccionar documentos'),
      file,
    );

    expect(onFilesSelected).toHaveBeenCalledWith([file]);
  });

  it('selects a dropped file', () => {
    const onFilesSelected = vi.fn();
    const file = new File(['contenido'], 'escaneo.png', {
      type: 'image/png',
    });

    render(<DocumentDropzone onFilesSelected={onFilesSelected} />);

    fireEvent.drop(screen.getByRole('group', { name: 'Carga de documentos' }), {
      dataTransfer: { files: [file] },
    });

    expect(onFilesSelected).toHaveBeenCalledWith([file]);
  });

  it('opens the file picker from the keyboard-accessible button', async () => {
    const user = userEvent.setup();
    const clickSpy = vi.spyOn(HTMLInputElement.prototype, 'click');

    render(<DocumentDropzone onFilesSelected={vi.fn()} />);

    const button = screen.getByRole('button', { name: 'Seleccionar archivos' });
    button.focus();
    await user.keyboard('{Enter}');

    expect(clickSpy).toHaveBeenCalledTimes(1);
  });

  it('shows the accepted formats and size limit', () => {
    render(<DocumentDropzone onFilesSelected={vi.fn()} />);

    expect(screen.getByText(/PDF, JPG, PNG/)).toBeInTheDocument();
    expect(screen.getByText(/Máximo 50 MB/)).toBeInTheDocument();
  });

  it('ignores files while disabled', () => {
    const onFilesSelected = vi.fn();
    const file = new File(['contenido'], 'factura.pdf', {
      type: 'application/pdf',
    });

    render(<DocumentDropzone disabled onFilesSelected={onFilesSelected} />);

    expect(
      screen.getByLabelText('Seleccionar documentos'),
    ).toBeDisabled();
    expect(
      screen.getByRole('button', { name: 'Seleccionar archivos' }),
    ).toBeDisabled();

    fireEvent.drop(screen.getByRole('group', { name: 'Carga de documentos' }), {
      dataTransfer: { files: [file] },
    });

    expect(onFilesSelected).not.toHaveBeenCalled();
  });
});
