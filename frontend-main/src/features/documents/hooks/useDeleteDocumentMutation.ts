import { useMutation, useQueryClient } from '@tanstack/react-query';

import { documentsApi } from '../api/documentsApi';
import { documentKeys } from '../api/queryKeys';

export function useDeleteDocumentMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (documentId: string) => documentsApi.remove(documentId),
    onSuccess: (_result, documentId) => {
      queryClient.removeQueries({
        queryKey: documentKeys.detail(documentId),
      });

      return queryClient.invalidateQueries({
        queryKey: documentKeys.lists(),
      });
    },
  });
}
