import { renderHook, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { useDocumentsQuery } from '@/features/documents/hooks/useDocumentsQuery';

import {
  createQueryClientWrapper,
  createTestQueryClient,
} from '../utils/queryClient';

describe('useDocumentsQuery', () => {
  it('loads the paginated document list', async () => {
    const queryClient = createTestQueryClient();
    const { result } = renderHook(() => useDocumentsQuery(), {
      wrapper: createQueryClientWrapper(queryClient),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toMatchObject({
      page: 1,
      pageSize: 20,
      total: 2,
    });
    expect(
      result.current.data?.items.map((document) => document.fileName),
    ).toEqual(['factura-ejemplo.pdf', 'escaneo-ilegible.png']);
  });

  it('filters documents by status', async () => {
    const queryClient = createTestQueryClient();
    const { result } = renderHook(
      () => useDocumentsQuery({ status: 'failed' }),
      { wrapper: createQueryClientWrapper(queryClient) },
    );

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data?.total).toBe(1);
    expect(result.current.data?.items[0]).toMatchObject({
      fileName: 'escaneo-ilegible.png',
      status: 'failed',
      error: { code: 'OCR_PROCESSING_FAILED' },
    });
  });
});
