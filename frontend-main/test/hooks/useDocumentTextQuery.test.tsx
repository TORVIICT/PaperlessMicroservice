import { renderHook, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { useDocumentTextQuery } from '@/features/documents/hooks/useDocumentTextQuery';

import {
  createQueryClientWrapper,
  createTestQueryClient,
} from '../utils/queryClient';

const COMPLETED_ID = '11111111-1111-4111-8111-111111111111';

describe('useDocumentTextQuery', () => {
  it('loads the extracted text when enabled', async () => {
    const queryClient = createTestQueryClient();
    const { result } = renderHook(() => useDocumentTextQuery(COMPLETED_ID), {
      wrapper: createQueryClientWrapper(queryClient),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data?.text).toContain('Factura de ejemplo');
    expect(result.current.data?.pageCount).toBe(2);
  });

  it('does not fetch while disabled', () => {
    const queryClient = createTestQueryClient();
    const { result } = renderHook(
      () => useDocumentTextQuery(COMPLETED_ID, { enabled: false }),
      { wrapper: createQueryClientWrapper(queryClient) },
    );

    expect(result.current.fetchStatus).toBe('idle');
    expect(result.current.isPending).toBe(true);
  });
});
