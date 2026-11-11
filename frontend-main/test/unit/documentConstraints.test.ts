import { describe, expect, it } from 'vitest';

import {
  isSupportedContentType,
  MAX_FILE_SIZE_BYTES,
  MAX_FILE_SIZE_MB,
} from '@/features/documents/models/documentConstraints';

describe('documentConstraints', () => {
  it('accepts the content types required by the contract', () => {
    expect(isSupportedContentType('application/pdf')).toBe(true);
    expect(isSupportedContentType('image/jpeg')).toBe(true);
    expect(isSupportedContentType('image/png')).toBe(true);
    expect(isSupportedContentType('image/heic')).toBe(true);
  });

  it('rejects unsupported or empty content types', () => {
    expect(isSupportedContentType('text/plain')).toBe(false);
    expect(isSupportedContentType('')).toBe(false);
  });

  it('exposes the 50 MB limit as a single source of truth', () => {
    expect(MAX_FILE_SIZE_BYTES).toBe(50 * 1024 * 1024);
    expect(MAX_FILE_SIZE_MB).toBe(50);
  });
});
