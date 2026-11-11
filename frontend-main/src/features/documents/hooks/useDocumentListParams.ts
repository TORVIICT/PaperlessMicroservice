import { useCallback } from 'react';
import { useSearchParams } from 'react-router-dom';

import {
  buildDocumentListSearchParams,
  parseDocumentListState,
} from '../lib/listParams';
import type { DocumentStatus } from '../models/document';

export function useDocumentListParams() {
  const [searchParams, setSearchParams] = useSearchParams();
  const { page, status } = parseDocumentListState(searchParams);

  const setStatus = useCallback(
    (nextStatus: DocumentStatus | undefined) => {
      setSearchParams(() =>
        buildDocumentListSearchParams({ status: nextStatus, page: 1 }),
      );
    },
    [setSearchParams],
  );

  const setPage = useCallback(
    (nextPage: number) => {
      setSearchParams((currentSearchParams) => {
        const currentState = parseDocumentListState(currentSearchParams);

        return buildDocumentListSearchParams({
          status: currentState.status,
          page: nextPage,
        });
      });
    },
    [setSearchParams],
  );

  return { page, status, setPage, setStatus };
}
