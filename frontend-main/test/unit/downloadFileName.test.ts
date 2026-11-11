import { describe, expect, it } from 'vitest';

import { resolveDownloadFileName } from '@/features/documents/lib/downloadFileName';

describe('resolveDownloadFileName', () => {
  it('keeps the original name for the original file', () => {
    expect(resolveDownloadFileName('factura.pdf', 'original')).toBe(
      'factura.pdf',
    );
  });

  it('builds a TXT name from the original name', () => {
    expect(resolveDownloadFileName('factura.pdf', 'text')).toBe(
      'factura.txt',
    );
  });

  it('replaces only the last extension', () => {
    expect(resolveDownloadFileName('factura.2026.01.png', 'text')).toBe(
      'factura.2026.01.txt',
    );
  });

  it('handles file names without an extension', () => {
    expect(resolveDownloadFileName('factura', 'text')).toBe(
      'factura.txt',
    );
  });
});
