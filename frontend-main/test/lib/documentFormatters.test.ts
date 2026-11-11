import { describe, expect, it } from 'vitest';

import {
  formatContentType,
  formatContentTypeList,
  formatDocumentDate,
  formatFileSize,
  formatOptionalDocumentDate,
  formatPageCount,
} from '@/features/documents/lib/documentFormatters';

describe('formatFileSize', () => {
  it('formats values below one kilobyte as bytes', () => {
    expect(formatFileSize(0)).toBe('0 B');
    expect(formatFileSize(1)).toBe('1 B');
    expect(formatFileSize(1023)).toBe('1023 B');
  });

  it('formats kilobytes and megabytes with a Spanish decimal separator', () => {
    expect(formatFileSize(1024)).toBe('1 kB');
    expect(formatFileSize(182_400)).toBe('178,1 kB');
    expect(formatFileSize(1_572_864)).toBe('1,5 MB');
    expect(formatFileSize(1024 ** 3)).toBe('1 GB');
  });

  it('returns a placeholder for invalid values', () => {
    expect(formatFileSize(-1)).toBe('—');
    expect(formatFileSize(Number.NaN)).toBe('—');
    expect(formatFileSize(Number.POSITIVE_INFINITY)).toBe('—');
  });
});

describe('formatDocumentDate', () => {
  it('formats a valid ISO date', () => {
    const result = formatDocumentDate('2026-01-15T12:00:00.000Z');

    expect(result).not.toBe('—');
    expect(result).toContain('2026');
  });

  it('returns a placeholder for invalid dates', () => {
    expect(formatDocumentDate('not-a-date')).toBe('—');
    expect(formatDocumentDate('')).toBe('—');
  });
});

describe('formatOptionalDocumentDate', () => {
  it('formats present dates and marks missing ones', () => {
    expect(formatOptionalDocumentDate('2026-01-15T12:00:00.000Z')).toContain(
      '2026',
    );
    expect(formatOptionalDocumentDate(null)).toBe('—');
  });
});

describe('formatPageCount', () => {
  it('uses the singular and plural form', () => {
    expect(formatPageCount(1)).toBe('1 página');
    expect(formatPageCount(2)).toBe('2 páginas');
  });

  it('returns a placeholder for missing or invalid values', () => {
    expect(formatPageCount(null)).toBe('—');
    expect(formatPageCount(-1)).toBe('—');
    expect(formatPageCount(1.5)).toBe('—');
  });
});

describe('formatContentType', () => {
  it('maps supported MIME types to short labels', () => {
    expect(formatContentType('application/pdf')).toBe('PDF');
    expect(formatContentType('image/jpeg')).toBe('JPG');
    expect(formatContentType('image/heic')).toBe('HEIC');
  });

  it('keeps unknown types and handles empty values', () => {
    expect(formatContentType('application/octet-stream')).toBe(
      'application/octet-stream',
    );
    expect(formatContentType('')).toBe('—');
  });
});

describe('formatContentTypeList', () => {
  it('joins labels in the provided order', () => {
    expect(
      formatContentTypeList(['application/pdf', 'image/jpeg', 'image/png']),
    ).toBe('PDF, JPG, PNG');
  });
});
