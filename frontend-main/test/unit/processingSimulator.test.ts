import { describe, expect, it } from 'vitest';

import { resolveProcessingState } from '@/mocks/data/processingSimulator';
import type { ProcessingSource } from '@/mocks/data/processingSimulator';

const CREATED_AT = '2026-01-01T00:00:00.000Z';
const NOW = Date.parse(CREATED_AT);

const baseSource: ProcessingSource = {
  fileName: 'factura.pdf',
  createdAt: CREATED_AT,
  outcome: 'completed',
  alreadyProcessed: false,
  completedAt: null,
  pageCount: null,
  extractedText: '',
};

describe('resolveProcessingState', () => {
  it('starts in queued state', () => {
    expect(resolveProcessingState(baseSource, NOW).status).toBe('queued');
  });

  it('transitions to processing after the queued window', () => {
    const state = resolveProcessingState(baseSource, NOW + 3_000);

    expect(state.status).toBe('processing');
    expect(state.completedAt).toBeNull();
  });

  it('completes with derived metadata', () => {
    const state = resolveProcessingState(baseSource, NOW + 7_000);

    expect(state.status).toBe('completed');
    expect(state.completedAt).toBe('2026-01-01T00:00:06.000Z');
    expect(state.pageCount).toBe(1);
    expect(state.error).toBeNull();
    expect(state.extractedText).toContain('factura.pdf');
  });

  it('fails with a processing error', () => {
    const state = resolveProcessingState(
      { ...baseSource, outcome: 'failed' },
      NOW + 7_000,
    );

    expect(state.status).toBe('failed');
    expect(state.error?.code).toBe('OCR_PROCESSING_FAILED');
    expect(state.extractedText).toBe('');
  });

  it('returns the final state immediately for seeded documents', () => {
    const state = resolveProcessingState(
      {
        ...baseSource,
        alreadyProcessed: true,
        completedAt: CREATED_AT,
        pageCount: 2,
        extractedText: 'Texto de ejemplo',
      },
      NOW,
    );

    expect(state.status).toBe('completed');
    expect(state.pageCount).toBe(2);
    expect(state.extractedText).toBe('Texto de ejemplo');
  });

  it('keeps seeded failures in failed state', () => {
    const state = resolveProcessingState(
      {
        ...baseSource,
        outcome: 'failed',
        alreadyProcessed: true,
        completedAt: CREATED_AT,
      },
      NOW,
    );

    expect(state.status).toBe('failed');
    expect(state.error?.code).toBe('OCR_PROCESSING_FAILED');
  });
});
