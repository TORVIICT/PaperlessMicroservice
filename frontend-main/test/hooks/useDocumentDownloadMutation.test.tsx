import { act, renderHook } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { useDocumentDownloadMutation } from '@/features/documents/hooks/useDocumentDownloadMutation';
import { saveBlob } from '@/features/documents/lib/saveBlob';

import {
  createQueryClientWrapper,
  createTestQueryClient,
} from '../utils/queryClient';

vi.mock('@/features/documents/lib/saveBlob', () => ({
  saveBlob: vi.fn(),
}));

const COMPLETED_ID = '11111111-1111-4111-8111-111111111111';
const DOCUMENT = {
  id: COMPLETED_ID,
  fileName: 'factura-ejemplo.pdf',
};

describe('useDocumentDownloadMutation', () => {
  it('downloads the original with its original file name', async () => {
    const queryClient = createTestQueryClient();
    const { result } = renderHook(() => useDocumentDownloadMutation(), {
      wrapper: createQueryClientWrapper(queryClient),
    });

    await act(async () => {
      await result.current.mutateAsync({
        document: DOCUMENT,
        variant: 'original',
      });
    });

    expect(saveBlob).toHaveBeenCalledWith(
      expect.any(Blob),
      'factura-ejemplo.pdf',
    );
  });

  it('downloads the extracted text with a derived TXT file name', async () => {
    const queryClient = createTestQueryClient();
    const { result } = renderHook(() => useDocumentDownloadMutation(), {
      wrapper: createQueryClientWrapper(queryClient),
    });

    await act(async () => {
      await result.current.mutateAsync({
        document: DOCUMENT,
        variant: 'text',
      });
    });

    expect(saveBlob).toHaveBeenCalledWith(
      expect.any(Blob),
      'factura-ejemplo.txt',
    );
  });
});
