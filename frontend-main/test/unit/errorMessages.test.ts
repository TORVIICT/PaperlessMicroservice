import { describe, expect, it } from 'vitest';

import {
  getUserFacingMessage,
  toUserFacingError,
} from '@/features/documents/lib/errorMessages';
import { ApiError } from '@/shared/api/ApiError';

function createApiError(status: number, code: string, message = 'Mensaje del backend') {
  return new ApiError(status, {
    code,
    message,
    documentId: null,
    details: {},
  });
}

describe('toUserFacingError', () => {
  it('maps contract codes to friendly messages', () => {
    const result = toUserFacingError(createApiError(413, 'FILE_TOO_LARGE'));

    expect(result.code).toBe('FILE_TOO_LARGE');
    expect(result.message).toContain('tamaño máximo');
    expect(result.status).toBe(413);
  });

  it('keeps the technical code visible for diagnosis', () => {
    const result = toUserFacingError(createApiError(409, 'DOCUMENT_NOT_READY'));

    expect(result.code).toBe('DOCUMENT_NOT_READY');
    expect(result.message).toContain('todavía se está procesando');
  });

  it('falls back to the backend message for unknown codes', () => {
    const result = toUserFacingError(
      createApiError(400, 'CUSTOM_BACKEND_ERROR', 'Descripción específica'),
    );

    expect(result.code).toBe('CUSTOM_BACKEND_ERROR');
    expect(result.message).toBe('Descripción específica');
  });

  it('maps unexpected 5xx errors to SERVICE_UNAVAILABLE', () => {
    const result = toUserFacingError(
      createApiError(503, 'UNEXPECTED_ERROR'),
    );

    expect(result.code).toBe('SERVICE_UNAVAILABLE');
    expect(result.message).toContain('no está disponible');
  });

  it('handles errors that are not ApiError', () => {
    const result = toUserFacingError(new Error('boom'));

    expect(result).toEqual({
      code: 'UNEXPECTED_ERROR',
      message: 'Ocurrió un error inesperado. Intente nuevamente.',
      status: null,
    });
  });
});

describe('getUserFacingMessage', () => {
  it('resolves messages for known contract codes', () => {
    expect(getUserFacingMessage('UNSUPPORTED_FILE_TYPE')).toContain(
      'formato',
    );
    expect(getUserFacingMessage('FILE_TOO_LARGE')).toContain(
      'tamaño máximo',
    );
  });

  it('uses the unexpected error message as a fallback', () => {
    expect(getUserFacingMessage('UNKNOWN_CODE')).toContain(
      'error inesperado',
    );
  });
});
