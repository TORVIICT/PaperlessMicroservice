import { describe, expect, it } from 'vitest';

import { isDocumentReadyForResults } from '@/features/documents/lib/documentAvailability';

describe('isDocumentReadyForResults', () => {
  it('enables results only for completed documents', () => {
    expect(isDocumentReadyForResults('completed')).toBe(true);
    expect(isDocumentReadyForResults('queued')).toBe(false);
    expect(isDocumentReadyForResults('processing')).toBe(false);
    expect(isDocumentReadyForResults('failed')).toBe(false);
  });
});
