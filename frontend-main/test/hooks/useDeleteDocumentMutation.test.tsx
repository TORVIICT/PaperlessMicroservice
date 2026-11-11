import { act, renderHook, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { documentKeys } from '@/features/documents/api/queryKeys';
import { documentsApi } from '@/features/documents/api/documentsApi';
import { useDeleteDocumentMutation } from '@/features/documents/hooks/useDeleteDocumentMutation';
import { useDocumentsQuery } from '@/features/documents/hooks/useDocumentsQuery';

import {
  createQueryClientWrapper,
  createTestQueryClient,
} from '../utils/queryClient';

const FAILED_ID = '22222222-2222-4222-8222-222222222222';

describe('useDeleteDocumentMutation', () => {
  it('deletes a document and clears its cached detail', async () => {
    const queryClient = createTestQueryClient();
    const wrapper = createQueryClientWrapper(queryClient);

    await queryClient.prefetchQuery({
      queryKey: documentKeys.detail(FAILED_ID),
      queryFn: () => documentsApi.getById(FAILED_ID),
    });
    expect(
      queryClient.getQueryState(documentKeys.detail(FAILED_ID)),
    ).toBeDefined();

    const list = renderHook(() => useDocumentsQuery(), { wrapper });
    await waitFor(() =>
      expect(list.result.current.data?.total).toBe(2),
    );

    const mutation = renderHook(() => useDeleteDocumentMutation(), {
      wrapper,
    });

    await act(async () => {
      await mutation.result.current.mutateAsync(FAILED_ID);
    });

    await waitFor(() =>
      expect(list.result.current.data?.total).toBe(1),
    );
    expect(
      queryClient.getQueryState(documentKeys.detail(FAILED_ID)),
    ).toBeUndefined();
  });
});
