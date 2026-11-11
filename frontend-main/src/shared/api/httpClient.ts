import { env } from '@/shared/config/env';

import {
  ApiError,
  NETWORK_ERROR_STATUS,
  type ApiErrorPayload,
} from './ApiError';

const DEFAULT_ERROR: ApiErrorPayload = {
  code: 'UNEXPECTED_ERROR',
  message: 'Ocurrió un error inesperado al comunicarse con el servidor.',
  documentId: null,
  details: {},
};

const SERVICE_UNAVAILABLE_ERROR: ApiErrorPayload = {
  code: 'SERVICE_UNAVAILABLE',
  message:
    'No fue posible comunicarse con el servicio OCR. Verifique su conexión e intente nuevamente.',
  documentId: null,
  details: {},
};

function buildUrl(path: string) {
  const baseUrl = env.apiBaseUrl.replace(/\/$/, '');
  const normalizedPath = path.replace(/^\//, '');
  return `${baseUrl}/${normalizedPath}`;
}

function isAbortError(error: unknown): boolean {
  return (
    typeof error === 'object' &&
    error !== null &&
    'name' in error &&
    error.name === 'AbortError'
  );
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function toErrorPayload(value: unknown): ApiErrorPayload | null {
  if (
    !isRecord(value) ||
    typeof value.code !== 'string' ||
    typeof value.message !== 'string'
  ) {
    return null;
  }

  return {
    code: value.code,
    message: value.message,
    documentId: typeof value.documentId === 'string' ? value.documentId : null,
    details: isRecord(value.details) ? value.details : {},
  };
}

async function readErrorPayload(
  response: Response,
): Promise<ApiErrorPayload | null> {
  try {
    return toErrorPayload(await response.json());
  } catch {
    return null;
  }
}

async function parseError(response: Response): Promise<ApiError> {
  const payload = await readErrorPayload(response);
  const fallback =
    response.status >= 500 ? SERVICE_UNAVAILABLE_ERROR : DEFAULT_ERROR;

  return new ApiError(response.status, payload ?? fallback);
}

async function request(path: string, init?: RequestInit): Promise<Response> {
  let response: Response;

  try {
    response = await fetch(buildUrl(path), init);
  } catch (error) {
    if (isAbortError(error)) {
      throw error;
    }

    throw new ApiError(NETWORK_ERROR_STATUS, SERVICE_UNAVAILABLE_ERROR);
  }

  if (!response.ok) {
    throw await parseError(response);
  }

  return response;
}

function withJsonAccept(init?: RequestInit): RequestInit {
  const headers = new Headers(init?.headers);

  if (!headers.has('Accept')) {
    headers.set('Accept', 'application/json');
  }

  return { ...init, headers };
}

export const httpClient = Object.freeze({
  async json<T>(path: string, init?: RequestInit): Promise<T> {
    const response = await request(path, withJsonAccept(init));
    return response.json() as Promise<T>;
  },

  async blob(path: string, init?: RequestInit): Promise<Blob> {
    const response = await request(path, init);
    return response.blob();
  },

  async empty(path: string, init?: RequestInit): Promise<void> {
    await request(path, init);
  },
});
