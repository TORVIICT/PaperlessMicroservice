import { renderHook, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { useDocumentQuery } from '@/features/documents/hooks/useDocumentQuery';

import {
  createQueryClientWrapper,
  createTestQueryClient,
} from '../utils/queryClient';

const COMPLETED_ID = '11111111-1111-4111-8111-111111111111';
const FAILED_ID = '22222222-2222-4222-8222-222222222222';

describe('useDocumentQuery', () => {
  it('loads a completed document', async () => {
    const queryClient = createTestQueryClient();
    const { result } = renderHook(() => useDocumentQuery(COMPLETED_ID), {
      wrapper: createQueryClientWrapper(queryClient),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toMatchObject({
      fileName: 'factura-ejemplo.pdf',
      status: 'completed',
      pageCount: 2,
    });
  });

  it('loads a failed document with its processing error', async () => {
    const queryClient = createTestQueryClient();
    const { result } = renderHook(() => useDocumentQuery(FAILED_ID), {
      wrapper: createQueryClientWrapper(queryClient),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toMatchObject({
      status: 'failed',
      error: { code: 'OCR_PROCESSING_FAILED' },
    });
  });
});
