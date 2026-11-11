import { describe, expect, it } from 'vitest';

import { validateDocumentFile } from '@/features/documents/lib/fileValidation';
import { MAX_FILE_SIZE_BYTES } from '@/features/documents/models/documentConstraints';

interface CreateFileOptions {
  name?: string;
  type?: string;
  size?: number;
}

function createFile({
  name = 'documento.pdf',
  type = 'application/pdf',
  size = 1024,
}: CreateFileOptions = {}) {
  const file = new File(['contenido'], name, { type });
  Object.defineProperty(file, 'size', { value: size });

  return file;
}

describe('validateDocumentFile', () => {
  it('accepts a supported file within the size limit', () => {
    expect(validateDocumentFile(createFile())).toBeNull();
    expect(
      validateDocumentFile(
        createFile({ name: 'escaneo.png', type: 'image/png' }),
      ),
    ).toBeNull();
  });

  it('accepts a file exactly at the size limit', () => {
    expect(
      validateDocumentFile(createFile({ size: MAX_FILE_SIZE_BYTES })),
    ).toBeNull();
  });

  it('rejects empty files', () => {
    expect(validateDocumentFile(createFile({ size: 0 }))).toBe(
      'INVALID_FILE',
    );
  });

  it('rejects unsupported content types', () => {
    expect(
      validateDocumentFile(
        createFile({ name: 'script.exe', type: 'application/x-msdownload' }),
      ),
    ).toBe('UNSUPPORTED_FILE_TYPE');
  });

  it('rejects files above the size limit', () => {
    expect(
      validateDocumentFile(createFile({ size: MAX_FILE_SIZE_BYTES + 1 })),
    ).toBe('FILE_TOO_LARGE');
  });
});
