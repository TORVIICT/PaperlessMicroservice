import { useEffect } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';

import { documentsApi } from '../api/documentsApi';
import { documentKeys } from '../api/queryKeys';
import { resolveDocumentRefetchInterval } from '../lib/polling';
import type { DocumentPage } from '../models/document';

export function useDocumentQuery(documentId: string) {
  const queryClient = useQueryClient();
  const query = useQuery({
    queryKey: documentKeys.detail(documentId),
    queryFn: ({ signal }) => documentsApi.getById(documentId, signal),
    enabled: Boolean(documentId),
    // A document can advance while only the list is mounted. Always refresh
    // when opening its detail so a previously cached queued state is not shown.
    refetchOnMount: 'always',
    refetchInterval: (query) =>
      resolveDocumentRefetchInterval(query.state.data?.status),
  });

  useEffect(() => {
    if (!query.data) return;
    const currentDocument = query.data;

    queryClient.setQueriesData<DocumentPage>(
      { queryKey: documentKeys.lists() },
      (page) => {
        if (!page?.items.some((document) => document.id === currentDocument.id)) {
          return page;
        }

        return {
          ...page,
          items: page.items.map((document) =>
            document.id === currentDocument.id ? currentDocument : document,
          ),
        };
      },
    );
  }, [query.data, queryClient]);

  return query;
}
