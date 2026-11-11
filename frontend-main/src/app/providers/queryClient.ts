import { QueryClient } from '@tanstack/react-query';

import { ApiError, NETWORK_ERROR_STATUS } from '@/shared/api/ApiError';

const MAX_QUERY_RETRIES = 1;

function shouldRetry(failureCount: number, error: unknown) {
  if (failureCount >= MAX_QUERY_RETRIES) {
    return false;
  }

  if (error instanceof ApiError) {
    return error.status === NETWORK_ERROR_STATUS || error.status >= 500;
  }

  return true;
}

export function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 30_000,
        retry: shouldRetry,
        refetchOnWindowFocus: false,
      },
    },
  });
}
