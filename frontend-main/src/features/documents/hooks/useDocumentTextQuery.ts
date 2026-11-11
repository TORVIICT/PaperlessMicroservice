import { useQuery } from '@tanstack/react-query';

import { documentsApi } from '../api/documentsApi';
import { documentKeys } from '../api/queryKeys';

export interface UseDocumentTextQueryOptions {
  enabled?: boolean;
}

export function useDocumentTextQuery(
  documentId: string,
  { enabled = true }: UseDocumentTextQueryOptions = {},
) {
  return useQuery({
    queryKey: documentKeys.text(documentId),
    queryFn: ({ signal }) => documentsApi.getText(documentId, signal),
    enabled: enabled && Boolean(documentId),
    retry: false,
  });
}
