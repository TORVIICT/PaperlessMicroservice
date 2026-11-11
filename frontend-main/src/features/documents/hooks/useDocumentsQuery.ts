import { useEffect } from 'react';
import {
  keepPreviousData,
  useQuery,
  useQueryClient,
} from '@tanstack/react-query';

import { documentsApi } from '../api/documentsApi';
import { documentKeys } from '../api/queryKeys';
import { resolveDocumentsRefetchInterval } from '../lib/polling';
import type { DocumentListParams } from '../models/document';

export function useDocumentsQuery(params: DocumentListParams = {}) {
  const queryClient = useQueryClient();
  const query = useQuery({
    queryKey: documentKeys.list(params),
    queryFn: ({ signal }) => documentsApi.list(params, signal),
    placeholderData: keepPreviousData,
    // Polling pauses while the tab is hidden. Refresh immediately when the
    // user returns so queued cards cannot remain stale after OCR completes.
    refetchOnWindowFocus: true,
    refetchInterval: (query) =>
      resolveDocumentsRefetchInterval(query.state.data?.items),
  });

  useEffect(() => {
    query.data?.items.forEach((document) => {
      queryClient.setQueryData(documentKeys.detail(document.id), document);
    });
  }, [query.data, queryClient]);

  return query;
}
