import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';

import { documentsApi } from '../api/documentsApi';
import { documentKeys } from '../api/queryKeys';
import type { OcrDocument } from '../models/document';

const MAX_CONCURRENT_UPLOADS = 3;

export type UploadItemStatus = 'pending' | 'uploading' | 'success' | 'error';

export interface UploadItem {
  id: string;
  file: File;
  status: UploadItemStatus;
  document?: OcrDocument;
  error?: unknown;
}

function createUploadItems(files: readonly File[]): UploadItem[] {
  return files.map((file, index) => ({
    id: `${file.name}-${file.size}-${file.lastModified}-${index}`,
    file,
    status: 'pending',
  }));
}

export function useUploadDocumentsMutation() {
  const queryClient = useQueryClient();
  const [items, setItems] = useState<UploadItem[]>([]);

  const mutation = useMutation({
    mutationFn: async (files: readonly File[]) => {
      const uploadItems = createUploadItems(files);
      setItems(uploadItems);
      let nextIndex = 0;

      async function uploadNext(): Promise<void> {
        while (nextIndex < uploadItems.length) {
          const item = uploadItems[nextIndex];
          nextIndex += 1;

          setItems((current) =>
            current.map((candidate) =>
              candidate.id === item.id
                ? { ...candidate, status: 'uploading' }
                : candidate,
            ),
          );

          try {
            const document = await documentsApi.upload(item.file);
            setItems((current) =>
              current.map((candidate) =>
                candidate.id === item.id
                  ? { ...candidate, status: 'success', document }
                  : candidate,
              ),
            );
          } catch (error) {
            setItems((current) =>
              current.map((candidate) =>
                candidate.id === item.id
                  ? { ...candidate, status: 'error', error }
                  : candidate,
              ),
            );
          }
        }
      }

      const workerCount = Math.min(
        MAX_CONCURRENT_UPLOADS,
        uploadItems.length,
      );
      await Promise.all(
        Array.from({ length: workerCount }, () => uploadNext()),
      );

      return uploadItems.length;
    },
    onSettled: () =>
      queryClient.invalidateQueries({ queryKey: documentKeys.lists() }),
  });

  function reset() {
    setItems([]);
    mutation.reset();
  }

  return { ...mutation, items, reset };
}
