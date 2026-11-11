import { describe, expect, it } from 'vitest';

import {
  buildDocumentListSearchParams,
  DOCUMENTS_PAGE_SIZE,
  isDocumentStatus,
  parseDocumentListState,
  toDocumentListParams,
} from '@/features/documents/lib/listParams';

describe('parseDocumentListState', () => {
  it('uses defaults when the URL has no parameters', () => {
    expect(parseDocumentListState(new URLSearchParams())).toEqual({ page: 1 });
  });

  it('reads a valid status and page', () => {
    expect(
      parseDocumentListState(
        new URLSearchParams('status=failed&page=3'),
      ),
    ).toEqual({ page: 3, status: 'failed' });
  });

  it('ignores invalid statuses and pages', () => {
    expect(
      parseDocumentListState(
        new URLSearchParams('status=unknown&page=0'),
      ),
    ).toEqual({ page: 1 });
    expect(
      parseDocumentListState(new URLSearchParams('page=abc')),
    ).toEqual({ page: 1 });
    expect(
      parseDocumentListState(new URLSearchParams('page=2.5')),
    ).toEqual({ page: 1 });
  });
});

describe('buildDocumentListSearchParams', () => {
  it('omits default values to keep the URL clean', () => {
    expect(
      buildDocumentListSearchParams({ page: 1 }).toString(),
    ).toBe('');
  });

  it('writes the status and page when needed', () => {
    expect(
      buildDocumentListSearchParams({
        page: 2,
        status: 'completed',
      }).toString(),
    ).toBe('status=completed&page=2');
  });
});

describe('toDocumentListParams', () => {
  it('uses the configured page size and omits an absent status', () => {
    expect(toDocumentListParams({ page: 1 })).toEqual({
      page: 1,
      pageSize: DOCUMENTS_PAGE_SIZE,
    });
  });

  it('includes the status when present', () => {
    expect(
      toDocumentListParams({ page: 2, status: 'processing' }),
    ).toEqual({
      page: 2,
      pageSize: DOCUMENTS_PAGE_SIZE,
      status: 'processing',
    });
  });
});

describe('isDocumentStatus', () => {
  it('recognizes only contract statuses', () => {
    expect(isDocumentStatus('queued')).toBe(true);
    expect(isDocumentStatus('failed')).toBe(true);
    expect(isDocumentStatus('unknown')).toBe(false);
    expect(isDocumentStatus('')).toBe(false);
  });
});
